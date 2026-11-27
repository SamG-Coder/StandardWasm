using PeLibrary;
using static PeLibrary.ImageBytes;

namespace PortableCompiler
{
    internal static class ReadOnlyStringBorrow
    {
        // The internal CLR method returns a writable CLI byref. The portable
        // general-use path lends only a readonly location. Fresh-string
        // construction binds its independently proven writes before linking.
        internal static bool IsOrdinalInput(byte[] image, int[] layout, int[] heaps, int caller, int row)
        {
            if (!MatchesAscii(image, heaps, (uint)MethodDefinitions.ReadColumn(image, layout, row, 3), "GetRawStringData"))
                return false;
            var counts = new int[45];
            int first = MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts);
            if (first < 0 || !PlatformAssemblyIdentity.IsCoreLibDefinition(image, heaps, layout, counts) ||
                (MethodDefinitions.ReadColumn(image, layout, row, 2) & 0x2457) != 3 ||
                !CoreStringRange.IsMethod(image, layout, heaps, counts, first, row, "System", "String",
                    "GetRawStringData", new byte[] { 0x20, 0, 0x10, 3 }, false))
                return false;
            var body = new int[6];
            uint rva = unchecked((uint)MethodDefinitions.ReadColumn(image, layout, row, 0));
            if (MethodBodies.Read(image, rva, body) != 0 || body[1] != 7 || body[3] != 0 || body[5] != 0)
                return false;
            uint start = (uint)body[0];
            uint field = U32(image, start + 2);
            if (U16(image, start) != 0x7c02 || image[start + 6] != 0x2a || field >> 24 != 4 ||
                !StringMetadata.IsFirstCodeUnitDefinition(image, heaps, layout, (int)(field & 0xffffff)))
                return false;

            // Both borrowed locations are consumed by the genuine Ordinal
            // method together with a code-unit count. The packed owner need
            // not model the physical terminator beyond that range.
            if (!CoreStringRange.IsMethod(image, layout, heaps, counts, first, caller, "System", "String",
                "EqualsOrdinalIgnoreCaseNoLengthCheck", new byte[] { 0, 2, 2, 0x0e, 0x0e }, true))
                return false;
            rva = unchecked((uint)MethodDefinitions.ReadColumn(image, layout, caller, 0));
            if (MethodBodies.Read(image, rva, body) != 0 || body[1] != 24 || body[3] != 0 || body[5] != 0)
                return false;
            start = (uint)body[0];
            uint raw = 0x06000000u | (uint)row;
            uint length = U32(image, start + 14);
            uint ordinal = U32(image, start + 19);
            uint lengthReceiver = U16(image, start + 12);
            return U16(image, start) == 0x6f02 && U16(image, start + 6) == 0x6f03 &&
                U32(image, start + 2) == raw && U32(image, start + 8) == raw &&
                (lengthReceiver == 0x6f02 || lengthReceiver == 0x6f03) && image[start + 18] == 0x28 &&
                image[start + 23] == 0x2a && length >> 24 == 6 && ordinal >> 24 == 6 &&
                StringMetadata.AccessorDefinition(image, heaps, layout, (int)(length & 0xffffff)) == StringMetadata.Length &&
                CoreStringRange.IsMethod(image, layout, heaps, counts, first, (int)(ordinal & 0xffffff),
                    "System.Globalization", "Ordinal", "EqualsIgnoreCase",
                    new byte[] { 0, 3, 2, 0x10, 3, 0x10, 3, 8 }, true);
        }
    }
}
