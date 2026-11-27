using static PeLibrary.ImageBytes;

namespace PeLibrary
{
    // The single CLI Assembly row identifies a loaded module independently of
    // its package basename. Definition keys remain in the original blob heap.
    internal static class AssemblyDefinitionRecord
    {
        internal static int Find(byte[] image, int[] heaps, int[] layout, int[] counts)
        {
            if (MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts) < 0 || counts[32] != 1)
                return -1;
            return MetadataTableRows.TableStart(heaps, layout, counts, 32);
        }

        internal static int NameIndex(byte[] image, int[] layout, uint row) =>
            Index(image, row + (uint)(16 + layout[3]), layout[2]);

        internal static int CultureIndex(byte[] image, int[] layout, uint row) =>
            Index(image, row + (uint)(16 + layout[3] + layout[2]), layout[2]);

        internal static int KeyIndex(byte[] image, int[] layout, uint row) =>
            Index(image, row + 16, layout[3]);

        private static int Index(byte[] image, uint at, int width) =>
            width == 2 ? (int)U16(image, at) : unchecked((int)U32(image, at));
    }
}
