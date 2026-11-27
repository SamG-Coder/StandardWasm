using static PeLibrary.ImageBytes;

namespace PeLibrary
{
    internal static class MonoAssemblyNameMetadata
    {
        // This marker occupies the deferred-virtual operand, not an IL opcode.
        internal const int GetNameSlot = -1;

        internal static int IdentifySlot(byte[] image, int[] layout, int[] heaps, int row)
        {
            if ((MethodDefinitions.ReadColumn(image, layout, row, 2) & 0x4657) != 0x46 ||
                !MatchesAscii(image, heaps, (uint)MethodDefinitions.ReadColumn(image, layout, row, 3), "GetName"))
                return 0;
            var counts = new int[45];
            int first = MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts);
            if (first < 0 || !PlatformAssemblyIdentity.IsCoreLibDefinition(image, heaps, layout, counts)) return 0;
            int owner = ObjectLayouts.Owner(image, heaps, layout, row, false);
            if (owner <= 0 || !ObjectLayouts.NamedDefinition(image, heaps, layout, counts, first,
                owner, "System.Reflection", "Assembly")) return 0;
            int signature = MethodDefinitions.ReadColumn(image, layout, row, 4);
            if (MatchesSignature(image, layout, heaps, counts, first, signature, false)) return 1;
            return MatchesSignature(image, layout, heaps, counts, first, signature, true) ? 2 : 0;
        }

        internal static int ResolveRuntimeBody(byte[] image, int[] layout, int[] heaps, int declaration, int arity)
        {
            if (IdentifySlot(image, layout, heaps, declaration) != arity) return -2;
            var counts = new int[45];
            int first = MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts);
            if (first < 0) return -1;
            int runtime = ObjectLayouts.FindTypeDefinition(image, heaps, layout, "System.Reflection", "RuntimeAssembly");
            if (runtime < 2) return -2;
            int assembly = ObjectLayouts.Owner(image, heaps, layout, declaration, false);
            var shape = new int[5];
            if (ObjectLayouts.ReadShape(image, heaps, layout, runtime, shape) != 0 || shape[3] != assembly << 2)
                return -2;
            var range = new int[2];
            if (VirtualSlotMetadata.ReadMethodRange(image, heaps, layout, runtime, range) != 0) return -1;

            int body = 0;
            for (int row = range[0]; row < range[1]; row++)
            {
                int name = MethodDefinitions.ReadColumn(image, layout, row, 3);
                int flags = MethodDefinitions.ReadColumn(image, layout, row, 2);
                if ((flags & 0x4557) != 0x46 || !MatchesAscii(image, heaps, (uint)name, "GetName") ||
                    !MatchesSignature(image, layout, heaps, counts, first,
                        MethodDefinitions.ReadColumn(image, layout, row, 4), true)) continue;
                if (!UsesMonoRuntimeAssembly(image, layout, heaps, runtime, row)) continue;
                if (body != 0) return -5;
                body = row;
            }
            if (body == 0) return -2;
            if (arity == 2) return body;
            return UsesOriginalDefault(image, layout, heaps, declaration) ? declaration : -2;
        }

        private static bool UsesOriginalDefault(byte[] image, int[] layout, int[] heaps, int row)
        {
            var body = new int[6];
            uint rva = unchecked((uint)MethodDefinitions.ReadColumn(image, layout, row, 0));
            if (MethodBodies.Read(image, rva, body) != 0 || body[1] != 8 || body[3] != 0 || body[5] != 0) return false;
            int at = body[0];
            uint target = U32(image, (uint)at + 3);
            return image[at] == 0x02 && image[at + 1] == 0x16 && image[at + 2] == 0x6f &&
                image[at + 7] == 0x2a && target >> 24 == 6 &&
                IdentifySlot(image, layout, heaps, (int)(target & 0xffffff)) == 2;
        }

        internal static int NativeRecord(byte[] image, int[] layout, int[] heaps, int caller, int leaf)
        {
            if (!IsAssemblyNameCreation(image, layout, heaps, caller) ||
                !MatchesAscii(image, heaps, (uint)MethodDefinitions.ReadColumn(image, layout, leaf, 3), "GetNativeName") ||
                MethodDefinitions.ReadColumn(image, layout, leaf, 0) != 0 ||
                MethodDefinitions.ReadColumn(image, layout, leaf, 1) != 0x1000 ||
                ObjectLayouts.Owner(image, heaps, layout, caller, false) !=
                    ObjectLayouts.Owner(image, heaps, layout, leaf, false)) return -2;
            var body = new int[6];
            uint rva = unchecked((uint)MethodDefinitions.ReadColumn(image, layout, caller, 0));
            if (MethodBodies.Read(image, rva, body) != 0 || body[1] != 24 || body[5] != 0) return -2;
            int at = body[0];
            uint target = U32(image, (uint)at + 7);
            uint fill = U32(image, (uint)at + 19);
            int fillRow = (int)(fill & 0xffffff);
            if (image[at] != 0x73 || image[at + 5] != 0x02 || image[at + 6] != 0x28 ||
                target != (0x06000000u | (uint)leaf) || image[at + 11] != 0x0a || image[at + 12] != 0x25 ||
                image[at + 13] != 0x06 || image[at + 14] != 0x03 || image[at + 15] != 0x17 ||
                image[at + 16] != 0x17 || image[at + 17] != 0x17 || image[at + 18] != 0x6f ||
                image[at + 23] != 0x2a || fill >> 24 != 6 ||
                ObjectLayouts.Owner(image, heaps, layout, fillRow, false) != ObjectLayouts.Owner(image, heaps, layout, caller, false) ||
                !MatchesAscii(image, heaps, (uint)MethodDefinitions.ReadColumn(image, layout, fillRow, 3), "FillName"))
                return -2;

            var cursor = new int[1];
            int end = SignatureReader.ReadStatic(image, heaps, MethodDefinitions.ReadColumn(image, layout, leaf, 4), 1, cursor);
            if (end < 0 || !SignatureReader.TakeElement(image, cursor, end, 0x0f) ||
                !SignatureReader.TakeElement(image, cursor, end, 0x11)) return -2;
            int type = CompressedUnsigned(image, cursor[0], end, cursor);
            if (type <= 0 || (type & 3) != 0 || !SignatureReader.TakeElement(image, cursor, end, 0x18) || cursor[0] != end)
                return -2;
            int found = ObjectLayouts.FindTypeDefinition(image, heaps, layout, "Mono", "MonoAssemblyName");
            return found == type >> 2 ? found : -2;
        }

        internal static bool IsRuntimeBody(byte[] image, int[] layout, int[] heaps, int row)
        {
            var counts = new int[45];
            int first = MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts);
            if (first < 0 || !PlatformAssemblyIdentity.IsCoreLibDefinition(image, heaps, layout, counts)) return false;
            int owner = ObjectLayouts.Owner(image, heaps, layout, row, false);
            int signature = MethodDefinitions.ReadColumn(image, layout, row, 4);
            return owner >= 2 && ObjectLayouts.NamedDefinition(image, heaps, layout, counts, first,
                owner, "System.Reflection", "RuntimeAssembly") &&
                MatchesSignature(image, layout, heaps, counts, first, signature, true) &&
                UsesMonoRuntimeAssembly(image, layout, heaps, owner, row);
        }

        private static bool UsesMonoRuntimeAssembly(byte[] image, int[] layout, int[] heaps, int owner, int row)
        {
            var body = new int[6];
            uint rva = unchecked((uint)MethodDefinitions.ReadColumn(image, layout, row, 0));
            if (MethodBodies.Read(image, rva, body) != 0 || body[1] != 19 || body[3] != 0 || body[5] != 0) return false;
            int at = body[0];
            uint field = U32(image, (uint)at + 2);
            uint info = U32(image, (uint)at + 9);
            uint create = U32(image, (uint)at + 14);
            return image[at] == 0x02 && image[at + 1] == 0x7b && field >> 24 == 4 &&
                IsMonoAssemblyHandle(image, layout, heaps, owner, (int)(field & 0xffffff)) &&
                image[at + 6] == 0x02 && image[at + 7] == 0x18 && image[at + 8] == 0x28 &&
                info >> 24 == 6 && IsCodeBaseWrapper(image, layout, heaps, (int)(info & 0xffffff), owner) &&
                image[at + 13] == 0x28 && create >> 24 == 6 &&
                IsAssemblyNameCreation(image, layout, heaps, (int)(create & 0xffffff)) &&
                image[at + 18] == 0x2a;
        }

        internal static bool IsMonoAssemblyHandle(byte[] image, int[] layout, int[] heaps, int owner, int row)
        {
            int table = AssemblyReferences.FieldTable(image, heaps, layout);
            if (table < 0 || ObjectLayouts.Owner(image, heaps, layout, row, true) != owner) return false;
            var fields = new int[3];
            AssemblyReferences.ReadField(image, layout, table, row, fields);
            int blob = fields[2];
            return blob > 0 && Fits((uint)blob, 3, (uint)heaps[1]) &&
                MatchesAscii(image, heaps, (uint)fields[1], "_mono_assembly") &&
                image[heaps[0] + blob] == 2 && image[heaps[0] + blob + 1] == 6 && image[heaps[0] + blob + 2] == 0x18;
        }

        private static bool IsCodeBaseWrapper(byte[] image, int[] layout, int[] heaps, int row, int owner)
        {
            if (ObjectLayouts.Owner(image, heaps, layout, row, false) != owner ||
                !MatchesAscii(image, heaps, (uint)MethodDefinitions.ReadColumn(image, layout, row, 3), "GetInfo")) return false;
            var cursor = new int[1];
            int end = SignatureReader.ReadInstance(image, heaps, MethodDefinitions.ReadColumn(image, layout, row, 4), 1, cursor);
            if (end < 0 || !SignatureReader.TakeElement(image, cursor, end, 0x0e) ||
                !SignatureReader.TakeElement(image, cursor, end, 0x11)) return false;
            int kind = CompressedUnsigned(image, cursor[0], end, cursor);
            if (kind <= 0 || (kind & 3) != 0 || cursor[0] != end) return false;
            var counts = new int[45];
            int first = MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts);
            var names = new int[2];
            if (first < 0 || kind >> 2 > counts[2] ||
                NestedTypes.Enclosing(image, heaps, layout, counts, kind >> 2) != owner ||
                ObjectLayouts.TypeDefinitionNames(image, heaps, layout, kind >> 2, names) != 0 ||
                !MatchesAscii(image, heaps, (uint)names[0], "AssemblyInfoKind") ||
                !HasCodeBaseKind(image, layout, heaps, kind >> 2)) return false;

            var body = new int[6];
            uint rva = unchecked((uint)MethodDefinitions.ReadColumn(image, layout, row, 0));
            if (MethodBodies.Read(image, rva, body) != 0 || body[1] != 26 || body[5] != 0) return false;
            int at = body[0];
            uint token = U32(image, (uint)at + 20);
            int leaf = (int)(token & 0xffffff);
            return image[at] == 0x02 && image[at + 1] == 0x0a && image[at + 2] == 0x14 &&
                image[at + 3] == 0x0b && image[at + 18] == 0x03 && image[at + 19] == 0x28 &&
                image[at + 24] == 0x07 && image[at + 25] == 0x2a && token >> 24 == 6 &&
                ObjectLayouts.Owner(image, heaps, layout, leaf, false) == owner &&
                MethodDefinitions.ReadColumn(image, layout, leaf, 0) == 0 &&
                MethodDefinitions.ReadColumn(image, layout, leaf, 1) == 0x1000 &&
                MatchesAscii(image, heaps, (uint)MethodDefinitions.ReadColumn(image, layout, leaf, 3), "GetInfo") &&
                IsGetInfoLeaf(image, layout, heaps, counts, first, leaf, kind);
        }

        private static bool HasCodeBaseKind(byte[] image, int[] layout, int[] heaps, int row)
        {
            var shape = new int[5];
            if (ObjectLayouts.ReadShape(image, heaps, layout, row, shape, true) < 0 ||
                shape[2] < 2 || shape[2] > 16) return false;
            var values = new ulong[shape[2]];
            var names = new int[shape[2]];
            var fields = new int[shape[2]];
            if (EnumLiteralMetadata.Read(image, heaps, layout, row, shape, values, names, fields) != SignatureReader.Int32)
                return false;
            for (int index = 0; index < names.Length; index++)
                if (names[index] < 0 && MatchesAscii(image, heaps, (uint)-names[index], "CodeBase"))
                    return values[index] == 2;
            return false;
        }

        private static bool IsGetInfoLeaf(byte[] image, int[] layout, int[] heaps, int[] counts, int first, int row, int kind)
        {
            var cursor = new int[1];
            int end = SignatureReader.ReadStatic(image, heaps, MethodDefinitions.ReadColumn(image, layout, row, 4), 3, cursor);
            return end >= 0 && SignatureReader.TakeElement(image, cursor, end, SignatureReader.Void) &&
                IsHandleType(image, layout, heaps, counts, first, cursor, end, "QCallAssembly") &&
                IsHandleType(image, layout, heaps, counts, first, cursor, end, "ObjectHandleOnStack") &&
                SignatureReader.TakeElement(image, cursor, end, 0x11) &&
                CompressedUnsigned(image, cursor[0], end, cursor) == kind && cursor[0] == end;
        }

        private static bool IsHandleType(byte[] image, int[] layout, int[] heaps, int[] counts, int first,
            int[] cursor, int end, string name)
        {
            if (!SignatureReader.TakeElement(image, cursor, end, 0x11)) return false;
            int type = CompressedUnsigned(image, cursor[0], end, cursor);
            return type > 0 && (type & 3) == 0 && type >> 2 <= counts[2] &&
                ObjectLayouts.NamedDefinition(image, heaps, layout, counts, first,
                    type >> 2, "System.Runtime.CompilerServices", name);
        }

        private static bool IsAssemblyNameCreation(byte[] image, int[] layout, int[] heaps, int row)
        {
            if (!MatchesAscii(image, heaps, (uint)MethodDefinitions.ReadColumn(image, layout, row, 3), "Create"))
                return false;
            var counts = new int[45];
            int first = MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts);
            int owner = ObjectLayouts.Owner(image, heaps, layout, row, false);
            if (first < 0 || owner <= 0 ||
                !ObjectLayouts.NamedDefinition(image, heaps, layout, counts, first, owner, "System.Reflection", "AssemblyName"))
                return false;
            var cursor = new int[1];
            int end = SignatureReader.ReadStatic(image, heaps, MethodDefinitions.ReadColumn(image, layout, row, 4), 2, cursor);
            return end >= 0 && SignatureReader.ReadClass(image, cursor, end) == owner << 2 &&
                SignatureReader.TakeElement(image, cursor, end, 0x18) &&
                SignatureReader.TakeElement(image, cursor, end, 0x0e) && cursor[0] == end;
        }

        private static bool MatchesSignature(byte[] image, int[] layout, int[] heaps, int[] counts,
            int first, int signature, bool flag)
        {
            var cursor = new int[1];
            int end = SignatureReader.ReadInstance(image, heaps, signature, flag ? 1 : 0, cursor);
            int type = end < 0 ? -1 : SignatureReader.ReadClass(image, cursor, end);
            return type > 0 && (type & 3) == 0 && ObjectLayouts.NamedDefinition(image, heaps, layout, counts, first,
                type >> 2, "System.Reflection", "AssemblyName") &&
                (!flag || SignatureReader.TakeElement(image, cursor, end, SignatureReader.Boolean)) && cursor[0] == end;
        }
    }
}
