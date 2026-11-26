using static PeLibrary.ImageBytes;

namespace PeLibrary
{
    internal static class TypeInitializers
    {
        private const int MethodAccess = 7, AssemblyAccess = 3, PublicAccess = 6;
        private const int Static = 0x10, HideBySignature = 0x80;
        private const int RuntimeSpecialName = 0x1000, SpecialName = 0x800;
        private const uint NestedVisibility = 6, Interface = 0x20, HasSecurity = 0x40000;

        internal static int CheckModuleInitializer(byte[] image, int[] layout, int[] heaps, int methodCount)
        {
            var range = new int[2];
            // TypeDef 1 is the CLI <Module> type.
            if (ObjectLayouts.MethodRange(image, heaps, layout, 1, range) < 0)
                return -1;
            int initializer = 0;
            for (int row = range[0]; row < range[1]; row++)
            {
                if (!MethodNames.IsInitializer(image, layout, heaps, row)) continue;
                if (initializer != 0 || !HasInitializerShape(image, layout, heaps, row))
                    return -2;
                initializer = row;
            }
            if (initializer == 0) return 0;

            var body = new int[6];
            if ((MethodDefinitions.ReadColumn(image, layout, initializer, 2) & ~(MethodAccess | Static | HideBySignature | RuntimeSpecialName | SpecialName)) != 0 ||
                !ReadPlainBody(image, layout, initializer, body))
                return -2;

            int start = body[0], end = start + body[1];
            int first = SkipNops(image, start, end);
            if (IsReturn(image, first, end)) return 0;

            // The SDK can emit a module constructor that calls a local empty
            // method. Prove both bodies and rule out a type-initialization
            // trigger before removing the call from the execution path.
            if (end - first < 6 || image[first] != 0x28 ||
                !IsReturn(image, SkipNops(image, first + 5, end), end))
                return -2;
            uint token = U32(image, (uint)first + 1);
            int target = (int)(token & 0xffffff);
            if (token >> 24 != 6 || target < 1 || target > methodCount ||
                MethodNames.IsInitializer(image, layout, heaps, target))
                return -2;

            int attributes = MethodDefinitions.ReadColumn(image, layout, target, 2);
            int access = attributes & MethodAccess;
            if ((attributes & ~(MethodAccess | Static | HideBySignature)) != 0 || (attributes & Static) == 0 ||
                (access != AssemblyAccess && access != PublicAccess) || !HasVoidSignature(image, layout, heaps, target))
                return -2;
            int owner = ObjectLayouts.Owner(image, heaps, layout, target, false);
            if (owner <= 1) return -2;

            var counts = new int[45];
            if (MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts) < 0)
                return -1;
            uint definition = (uint)TypeDefinition(layout, counts, owner);
            uint extends = definition + (uint)(4 + 2 * layout[2]);
            bool narrow = counts[2] < 16384 && counts[1] < 16384 && counts[27] < 16384;
            int baseType = MethodIndex(image, extends, narrow ? 2 : 4);
            // Limit this proof to top-level classes accessible to <Module>.
            if ((U32(image, definition) & (NestedVisibility | Interface | HasSecurity)) != 0 ||
                !ObjectLayouts.IsCoreObjectType(image, heaps, layout, baseType) ||
                GenericParameters.Check(image, heaps, layout, owner, new int[0], new int[0], new int[0]) != 0)
                return -2;
            int startup = RejectDeclaringType(image, layout, heaps, owner);
            if (startup < 0) return startup;
            if (!ReadPlainBody(image, layout, target, body)) return -2;
            end = body[0] + body[1];
            return IsReturn(image, SkipNops(image, body[0], end), end) ? 0 : -2;
        }

        private static bool ReadPlainBody(byte[] image, int[] layout, int row, int[] body) =>
            MethodDefinitions.ReadColumn(image, layout, row, 1) == 0 &&
            MethodBodies.Read(image, unchecked((uint)MethodDefinitions.ReadColumn(image, layout, row, 0)), body) == 0 &&
            body[3] == 0 && body[5] == 0;

        private static int SkipNops(byte[] image, int offset, int end)
        {
            while (offset < end && image[offset] == 0) offset++;
            return offset;
        }

        private static bool IsReturn(byte[] image, int offset, int end) =>
            end - offset == 1 && image[offset] == 0x2a;

        internal static int RejectDeclaringType(byte[] image, int[] layout, int[] heaps, int type, bool allowBeforeFieldInit = false)
        {
            var counts = new int[45];
            if (MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts) < 0 ||
                type < 1 || type > counts[2]) return -1;
            int strings = layout[2];
            int fields = counts[4] < 65536 ? 2 : 4;
            int methods = counts[6] < 65536 ? 2 : 4;
            int extends = counts[2] < 16384 && counts[1] < 16384 && counts[27] < 16384 ? 2 : 4;
            int methodColumn = 4 + 2 * strings + extends + fields;
            int rowWidth = methodColumn + methods;
            // CompressedTables already validated the TypeDef and Field tables.
            uint definition = (uint)TypeDefinition(layout, counts, type);
            uint at = definition + (uint)methodColumn;
            int first = MethodIndex(image, at, methods);
            int end = type == counts[2] ? counts[6] + 1 : MethodIndex(image, at + (uint)rowWidth, methods);
            if (first < 1 || end < first || end > counts[6] + 1) return -1;

            // ECMA-335 I.8.9.5 permits BeforeFieldInit to wait for the first
            // static field access. A supported non-triggering call can
            // skip this guard. The linker still checks every static field.
            bool mayDefer = allowBeforeFieldInit && (U32(image, definition) & 0x00100000) != 0;
            bool found = false;
            for (int row = first; row < end; row++)
            {
                if (!MethodNames.IsInitializer(image, layout, heaps, row)) continue;
                if (!mayDefer || found || !HasInitializerShape(image, layout, heaps, row)) return -2;
                found = true;
            }
            return 0;
        }

        // Stage 129 (decision 0057). Return the type's .cctor MethodDef row, or
        // zero when it has none. flags[0] receives 1 for a BeforeFieldInit type.
        // A second initializer or one with another shape is malformed for this
        // compiler and stays an explicit diagnostic.
        internal static int Find(byte[] image, int[] layout, int[] heaps, int type, int[] flags)
        {
            var counts = new int[45];
            if (MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts) < 0 ||
                type < 1 || type > counts[2]) return -1;
            int strings = layout[2];
            int fields = counts[4] < 65536 ? 2 : 4;
            int methods = counts[6] < 65536 ? 2 : 4;
            int extends = counts[2] < 16384 && counts[1] < 16384 && counts[27] < 16384 ? 2 : 4;
            int methodColumn = 4 + 2 * strings + extends + fields;
            int rowWidth = methodColumn + methods;
            uint definition = (uint)TypeDefinition(layout, counts, type);
            uint at = definition + (uint)methodColumn;
            int first = MethodIndex(image, at, methods);
            int end = type == counts[2] ? counts[6] + 1 : MethodIndex(image, at + (uint)rowWidth, methods);
            if (first < 1 || end < first || end > counts[6] + 1) return -1;
            flags[0] = (U32(image, definition) & 0x00100000) != 0 ? 1 : 0;
            int found = 0;
            for (int row = first; row < end; row++)
            {
                if (!MethodNames.IsInitializer(image, layout, heaps, row)) continue;
                if (found != 0 || !HasInitializerShape(image, layout, heaps, row)) return -2;
                found = row;
            }
            return found;
        }

        private static bool HasInitializerShape(byte[] image, int[] layout, int[] heaps, int row)
        {
            int attributes = MethodDefinitions.ReadColumn(image, layout, row, 2);
            return (attributes & 0x1810) == 0x1810 && HasVoidSignature(image, layout, heaps, row);
        }

        private static bool HasVoidSignature(byte[] image, int[] layout, int[] heaps, int row)
        {
            int blob = MethodDefinitions.ReadColumn(image, layout, row, 4);
            if (blob <= 0 || blob >= heaps[1]) return false;
            int limit = heaps[0] + heaps[1];
            var cursor = new int[1];
            int length = CompressedUnsigned(image, heaps[0] + blob, limit, cursor);
            int start = cursor[0];
            // Default static calling convention, zero parameters, void.
            return length == 3 && start <= limit - 3 &&
                image[start] == 0 && image[start + 1] == 0 && image[start + 2] == 1;
        }

        private static int TypeDefinition(int[] layout, int[] counts, int type)
        {
            int strings = layout[2];
            int fields = counts[4] < 65536 ? 2 : 4;
            int methods = counts[6] < 65536 ? 2 : 4;
            int extends = counts[2] < 16384 && counts[1] < 16384 && counts[27] < 16384 ? 2 : 4;
            int rowWidth = 4 + 2 * strings + extends + fields + methods;
            return layout[0] - counts[4] * (2 + strings + layout[3]) - (counts[2] - type + 1) * rowWidth;
        }

        private static int MethodIndex(byte[] image, uint offset, int width) =>
            width == 2 ? (int)U16(image, offset) : unchecked((int)U32(image, offset));
    }
}
