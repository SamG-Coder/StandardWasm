using static PeLibrary.ImageBytes;

namespace PeLibrary
{
    internal static class RuntimeTypeMetadata
    {
        internal const int Type = NativeStorage.Vector + 1, Handle = Type + 1, Module = Handle + 1, Assembly = Module + 1;

        internal static bool IsStorage(int type) => type == Handle || IsReference(type);
        internal static bool IsReference(int type) => type == Type || type == Module || type == Assembly;

        internal static int Storage(byte[] image, int[] heaps, int[] layout, int coded, bool valueType)
        {
            int kind = Kind(image, heaps, layout, coded);
            if (valueType) return kind == PlatformTypeReferences.TypeHandle ? Handle : 0;
            if (kind == PlatformTypeReferences.Type) return Type;
            if (kind == PlatformTypeReferences.Module) return Module;
            return kind == PlatformTypeReferences.Assembly ? Assembly : 0;
        }

        internal static bool RequiresReflectionCast(byte[] image, int[] heaps, int[] layout, int coded)
        {
            int kind = Kind(image, heaps, layout, coded);
            return kind == PlatformTypeReferences.Type || kind == PlatformTypeReferences.TypeBase ||
                kind == PlatformTypeReferences.Module || kind == PlatformTypeReferences.Assembly;
        }

        private static int Kind(byte[] image, int[] heaps, int[] layout, int coded)
        {
            if ((coded & 3) != 1) return 0;
            var counts = new int[45];
            int first = MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts);
            return first < 0 ? 0 : PlatformTypeReferences.Identify(image, heaps, layout, counts, first, coded >> 2);
        }

        // CoreLib constructors and fields refer to the concrete implementation
        // by TypeDef. The runtime-created object uses the same canonical
        // Assembly descriptor when retained by a managed resource stream.
        internal static bool IsRuntimeAssemblyDefinition(byte[] image, int[] heaps, int[] layout, int coded)
        {
            if ((coded & 3) != 0) return false;
            var counts = new int[45];
            int first = MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts);
            return first >= 0 &&
                ObjectLayouts.NamedDefinition(image, heaps, layout, counts, first, coded >> 2, "System.Reflection", "RuntimeAssembly") &&
                PlatformAssemblyIdentity.IsCoreLibDefinition(image, heaps, layout, counts);
        }

        internal static bool ValidDefinition(byte[] image, int[] heaps, int row)
        {
            // TypeDef row 1 is the module pseudo-type and has no runtime Type.
            if (row < 2) return false;
            var counts = new int[45];
            return MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts) >= 0 && row <= counts[2];
        }

        // Match the platform Object.GetType or a Type comparison operator.
        internal static int ObjectTypeMember(byte[] image, int[] heaps, int[] layout, int row)
        {
            var member = new int[AssemblyReferences.SymbolWidth];
            if (AssemblyReferences.ReadMember(image, heaps, layout, row, member) < 0 ||
                member[AssemblyReferences.Depth] != 0) return 0;
            int leaf = member[AssemblyReferences.Leaf];
            uint name = (uint)member[4];
            bool getType = MatchesAscii(image, heaps, name, "GetType") &&
                ObjectLayouts.IsCoreObjectType(image, heaps, layout, leaf << 2 | 1);
            bool equality = MatchesAscii(image, heaps, name, "op_Equality");
            bool inequality = MatchesAscii(image, heaps, name, "op_Inequality");
            if (!getType && !equality && !inequality) return 0;
            var counts = new int[45];
            int first = MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts);
            if (first < 0 || !getType &&
                PlatformTypeReferences.Identify(image, heaps, layout, counts, first, leaf) != PlatformTypeReferences.Type) return 0;
            int limit = heaps[0] + heaps[1], blob = member[5];
            if (blob <= 0 || blob >= heaps[1]) return 0;
            var next = new int[1];
            int bytes = CompressedUnsigned(image, heaps[0] + blob, limit, next);
            int start = next[0];
            if (bytes < 4 || bytes > limit - start) return 0;
            int end = start + bytes, cursor = start + 2;
            if (getType)
            {
                if (image[start] != 0x20 || image[start + 1] != 0) return 0;
            }
            else
            {
                if (image[start] != 0 || image[start + 1] != 2 || image[cursor] != 2) return 0;
                cursor++;
            }
            int types = getType ? 1 : 2;
            for (int index = 0; index < types; index++)
            {
                if (cursor >= end || image[cursor++] != 0x12) return 0;
                int token = CompressedUnsigned(image, cursor, end, next);
                if ((token & 3) != 1 || PlatformTypeReferences.Identify(image, heaps, layout, counts, first, token >> 2) != PlatformTypeReferences.Type)
                    return 0;
                cursor = next[0];
            }
            return cursor != end ? 0 : getType ? 1 : equality ? 2 : 3;
        }

        internal static bool IsGetTypeFromHandle(byte[] image, int[] heaps, int[] layout, int row)
        {
            var member = new int[AssemblyReferences.SymbolWidth];
            if (AssemblyReferences.ReadMember(image, heaps, layout, row, member) < 0 || member[AssemblyReferences.Depth] != 0 ||
                !PlatformTypeReferences.Named17(image, (uint)heaps[2], (uint)heaps[3], (uint)member[4],
                    0x54746547, 0x46657079, 0x486d6f72, 0x6c646e61, 0x65)) // GetTypeFromHandle
                return false;

            var counts = new int[45];
            int first = MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts);
            if (first < 0 || PlatformTypeReferences.Identify(image, heaps, layout, counts, first, member[AssemblyReferences.Leaf]) != PlatformTypeReferences.Type)
                return false;

            int blob = member[5], limit = heaps[0] + heaps[1];
            if (blob <= 0 || blob >= heaps[1]) return false;
            var next = new int[1];
            int bytes = CompressedUnsigned(image, heaps[0] + blob, limit, next);
            int start = next[0];
            if (bytes < 6 || bytes > limit - start || image[start] != 0 || image[start + 1] != 1 || image[start + 2] != 0x12)
                return false;
            int end = start + bytes;
            int result = CompressedUnsigned(image, start + 3, end, next);
            int cursor = next[0];
            if ((result & 3) != 1 || cursor >= end || image[cursor] != 0x11 ||
                PlatformTypeReferences.Identify(image, heaps, layout, counts, first, result >> 2) != PlatformTypeReferences.Type)
                return false;
            int parameter = CompressedUnsigned(image, cursor + 1, end, next);
            return next[0] == end && (parameter & 3) == 1 &&
                PlatformTypeReferences.Identify(image, heaps, layout, counts, first, parameter >> 2) == PlatformTypeReferences.TypeHandle;
        }
        internal static bool IsCoreGetTypeFromHandle(byte[] image, int[] heaps, int[] layout, int row)
        {
            if (!MatchesAscii(image, heaps, (uint)MethodDefinitions.ReadColumn(image, layout, row, 3), "GetTypeFromHandle")) return false;
            var counts = new int[45];
            int first = MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts);
            if (first < 0 || !PlatformAssemblyIdentity.IsCoreLibDefinition(image, heaps, layout, counts)) return false;
            int owner = ObjectLayouts.Owner(image, heaps, layout, row, false);
            if (owner < 1 || !ObjectLayouts.NamedDefinition(image, heaps, layout, counts, first, owner, "System", "Type")) return false;

            int blob = MethodDefinitions.ReadColumn(image, layout, row, 4), limit = heaps[0] + heaps[1];
            if (blob <= 0 || blob >= heaps[1]) return false;
            var next = new int[1];
            int size = CompressedUnsigned(image, heaps[0] + blob, limit, next);
            int start = next[0];
            if (size < 6 || size > limit - start || image[start] != 0 || image[start + 1] != 1 || image[start + 2] != 0x12)
                return false;
            int end = start + size;
            int result = CompressedUnsigned(image, start + 3, end, next);
            int cursor = next[0];
            if (result != owner << 2 || cursor >= end || image[cursor] != 0x11) return false;
            int parameter = CompressedUnsigned(image, cursor + 1, end, next);
            return parameter > 0 && (parameter & 3) == 0 && next[0] == end &&
                ObjectLayouts.NamedDefinition(image, heaps, layout, counts, first, parameter >> 2, "System", "RuntimeTypeHandle");
        }
        internal static bool IsValueTypeGetter(byte[] image, int[] heaps, int[] layout, int row, bool definition)
        {
            int signature;
            var counts = new int[45];
            int first = MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts);
            if (first < 0) return false;
            if (definition)
            {
                if (row < 1 || row > counts[6] || !PlatformAssemblyIdentity.IsCoreLibDefinition(image, heaps, layout, counts)) return false;
                int owner = ObjectLayouts.Owner(image, heaps, layout, row, false);
                if (owner < 1 || !ObjectLayouts.NamedDefinition(image, heaps, layout, counts, first, owner, "System", "Type") ||
                    !MatchesAscii(image, heaps, (uint)MethodDefinitions.ReadColumn(image, layout, row, 3), "get_IsValueType")) return false;
                signature = MethodDefinitions.ReadColumn(image, layout, row, 4);
            }
            else
            {
                var member = new int[AssemblyReferences.SymbolWidth];
                if (AssemblyReferences.ReadMember(image, heaps, layout, row, member) < 0 || member[AssemblyReferences.Depth] != 0 ||
                    !MatchesAscii(image, heaps, (uint)member[4], "get_IsValueType") ||
                    PlatformTypeReferences.Identify(image, heaps, layout, counts, first, member[AssemblyReferences.Leaf]) != PlatformTypeReferences.Type)
                    return false;
                signature = member[5];
            }
            if (signature < 1 || !Fits((uint)signature, 4, (uint)heaps[1])) return false;
            return U32(image, (uint)(heaps[0] + signature)) == 0x02002003; // instance bool (); length prefix 3
        }
    }
}
