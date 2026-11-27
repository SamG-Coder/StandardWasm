using static PeLibrary.ImageBytes;

namespace PeLibrary
{
    internal static class MonoLocaleMetadata
    {
        internal static bool IsPredefinedLocale(byte[] image, int[] heaps, int[] layout, int row)
        {
            int blob = MethodDefinitions.ReadColumn(image, layout, row, 4);
            if (blob <= 0 || blob > heaps[1] - 6 ||
                MethodDefinitions.ReadColumn(image, layout, row, 1) != 0x80) return false;
            int signature = heaps[0] + blob;
            // static int32 (uint16*). This is a loan of readable UTF-16,
            // and the real Mono wrapper retains ownership of the pin.
            if (U32(image, (uint)signature) != 0x08010005 ||
                U16(image, (uint)(signature + 4)) != 0x070f) return false;
            var counts = new int[45];
            int first = MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts);
            return first >= 0 && PlatformAssemblyIdentity.IsCoreLibDefinition(image, heaps, layout, counts) &&
                NativeImports.Find(image, heaps, layout, counts, "libSystem.Globalization.Native",
                    "GlobalizationNative_IsPredefinedLocale", row, 0x101) == row;
        }
    }
}
