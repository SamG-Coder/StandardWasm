using PeLibrary;
using static PeLibrary.ImageBytes;
using static PortableCompiler.BranchIl;

namespace PortableCompiler
{
    internal static class CoreStringPointer
    {
        internal static bool IsConstructor(byte[] image, int[] layout, int[] heaps, int row, bool member)
        {
            if (!member)
            {
                if (!IsInternalConstructor(image, layout, row)) return false;
                var counts = new int[45];
                int first = MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts);
                return first >= 0 && PlatformAssemblyIdentity.IsCoreLibDefinition(image, heaps, layout, counts) &&
                    IsPublicDefinition(image, layout, heaps, counts, first, row);
            }
            var reference = new int[AssemblyReferences.SymbolWidth];
            if (AssemblyReferences.ReadMember(image, heaps, layout, row, reference) < 0 ||
                !MatchesAscii(image, heaps, (uint)reference[4], ".ctor") ||
                !PlatformTypeReferences.IsString(image, heaps, layout, reference[AssemblyReferences.ParentType])) return false;
            var cursor = new int[1];
            int end = SignatureReader.ReadInstance(image, heaps, reference[5], 1, cursor);
            return end >= 0 && SignatureReader.TakeElement(image, cursor, end, SignatureReader.Void) &&
                SignatureReader.TakeElement(image, cursor, end, PointerStorage.Element) &&
                SignatureReader.TakeElement(image, cursor, end, SignatureReader.Character) && cursor[0] == end;
        }

        internal static int FindHelper(byte[] image, int[] layout, int[] heaps, int type)
        {
            var counts = new int[45];
            int first = MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts);
            if (first < 0 || !PlatformAssemblyIdentity.IsCoreLibDefinition(image, heaps, layout, counts)) return -2;
            var range = new int[2];
            int status = VirtualSlotMetadata.ReadMethodRange(image, heaps, layout, type, range);
            if (status < 0) return status;
            int helper = 0;
            bool found = false;
            for (int row = range[0]; row < range[1]; row++)
            {
                if (IsPublicDefinition(image, layout, heaps, counts, first, row))
                {
                    if (found) return -5;
                    found = true;
                }
                if (!IsHelper(image, layout, heaps, counts, first, row)) continue;
                if (helper != 0) return -5;
                helper = row;
            }
            return found && helper > 0 ? helper : -2;
        }

        private static bool IsPublicDefinition(byte[] image, int[] layout, int[] heaps, int[] counts, int first, int row) =>
            IsInternalConstructor(image, layout, row) &&
            CoreStringRange.IsMethod(image, layout, heaps, counts, first, row, "System", "String", ".ctor",
                new byte[] { 0x20, 1, 1, 0x0f, 3 }, false);

        private static bool IsInternalConstructor(byte[] image, int[] layout, int row) =>
            (MethodDefinitions.ReadColumn(image, layout, row, 2) & 0x1807) == 0x1806 &&
            MethodDefinitions.ReadColumn(image, layout, row, 0) == 0 &&
            MethodDefinitions.ReadColumn(image, layout, row, 1) == 0x1000;

        private static bool IsHelper(byte[] image, int[] layout, int[] heaps, int[] counts, int first, int row) =>
            (MethodDefinitions.ReadColumn(image, layout, row, 2) & 7) == 1 &&
            CoreStringRange.IsMethod(image, layout, heaps, counts, first, row, "System", "String", "Ctor",
                new byte[] { 0, 1, 0x0e, 0x0f, 3 }, true);

        internal static void Lower(byte[] image, int[] layout, int[] heaps, int method, int[] body, int[] records)
        {
            if (body[1] != 56 || body[5] != 0 ||
                !MatchesAscii(image, heaps, (uint)MethodDefinitions.ReadColumn(image, layout, method, 3), "Ctor")) return;
            var counts = new int[45];
            int first = MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts);
            if (first < 0 || !PlatformAssemblyIdentity.IsCoreLibDefinition(image, heaps, layout, counts) ||
                !IsHelper(image, layout, heaps, counts, first, method)) return;
            uint start = (uint)body[0];
            // The original branches return String.Empty before allocating. Keep
            // them and the fresh-string lifetime, adapting only native storage.
            if (U32(image, start) != 0x33e01602 || U16(image, start + 4) != 0x7e06 || U16(image, start + 10) != 0x022a ||
                image[start + 12] != 0x28 || image[start + 21] != 0x7e ||
                U32(image, start + 17) != 0x062d060a || U32(image, start + 26) != 0x28d3062a ||
                U16(image, start + 34) != 0x6f25 || U32(image, start + 40) != 0x7c250be0 ||
                U16(image, start + 48) != 0x0702 || image[start + 50] != 0x28 || image[start + 55] != 0x2a) return;
            uint scan = U32(image, start + 13), allocate = U32(image, start + 30), length = U32(image, start + 36);
            uint field = U32(image, start + 44), copy = U32(image, start + 51);
            if (scan >> 24 != 6 || allocate >> 24 != 6 || length >> 24 != 6 || field >> 24 != 4 ||
                !CoreStringRange.IsMethod(image, layout, heaps, counts, first, (int)(scan & 0xffffff),
                    "System", "String", "wcslen", new byte[] { 0, 1, 8, 0x0f, 3 }, true) ||
                !CoreStringRange.IsMethod(image, layout, heaps, counts, first, (int)(allocate & 0xffffff),
                    "System", "String", "FastAllocateString", new byte[] { 0, 1, 0x0e, 0x18 }, true) ||
                StringMetadata.AccessorDefinition(image, heaps, layout, (int)(length & 0xffffff)) != StringMetadata.Length ||
                !StringMetadata.IsFirstCodeUnitDefinition(image, heaps, layout, (int)(field & 0xffffff)) ||
                !CoreStringRange.IsCharacterMemmove(image, layout, heaps, counts, first, copy)) return;

            records[12 * Width] = NativeUtf16Length;
            records[29 * Width] = AllocateString;
            records[43 * Width] = Nop;
            records[50 * Width] = CopyNativeConstructionString;
        }
    }
}
