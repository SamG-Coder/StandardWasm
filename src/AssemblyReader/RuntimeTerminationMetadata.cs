using static PeLibrary.ImageBytes;
using static PeLibrary.SignatureReader;

namespace PeLibrary
{
    internal static class RuntimeTerminationMetadata
    {
        // Mono's managed wrappers carry the user message and exception to this
        // exact internal call. Keep CoreCLR's different native boundary explicit.
        internal static bool IsMonoFailFast(byte[] image, int[] heaps, int[] layout, int row)
        {
            if (MethodDefinitions.ReadColumn(image, layout, row, 0) != 0 ||
                MethodDefinitions.ReadColumn(image, layout, row, 1) != 0x1000 ||
                (MethodDefinitions.ReadColumn(image, layout, row, 2) & 0x10) == 0 ||
                !MatchesAscii(image, heaps, (uint)MethodDefinitions.ReadColumn(image, layout, row, 3), "FailFast"))
                return false;

            var counts = new int[45];
            int first = MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts);
            if (first < 0 || !PlatformAssemblyIdentity.IsCoreLibDefinition(image, heaps, layout, counts)) return false;
            int declaring = ObjectLayouts.Owner(image, heaps, layout, row, false);
            if (declaring < 1 || !ObjectLayouts.NamedDefinition(image, heaps, layout, counts, first, declaring, "System", "Environment"))
                return false;

            int blob = MethodDefinitions.ReadColumn(image, layout, row, 4);
            if (blob <= 0 || blob >= heaps[1]) return false;
            var next = new int[1];
            int size = CompressedUnsigned(image, heaps[0] + blob, heaps[0] + heaps[1], next);
            int at = next[0];
            if (size < 7 || size > heaps[0] + heaps[1] - at ||
                image[at] != 0 || image[at + 1] != 3 || image[at + 2] != 1 ||
                image[at + 3] != 0x0e || image[at + 4] != 0x12)
                return false;

            int end = at + size;
            int exception = CompressedUnsigned(image, at + 5, end, next);
            return exception > 0 && (exception & 3) == 0 && next[0] == end - 1 && image[next[0]] == 0x0e &&
                ObjectLayouts.NamedDefinition(image, heaps, layout, counts, first, exception >> 2, "System", "Exception");
        }
    }
}
