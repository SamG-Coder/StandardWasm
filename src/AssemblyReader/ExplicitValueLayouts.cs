using static PeLibrary.ImageBytes;

namespace PeLibrary
{
    internal static class ExplicitValueLayouts
    {
        // The current managed backing cell covers eight bytes. A zero
        // ClassLayout size leaves the extent to the declared field offsets.
        private const int BackingBytes = 8;

        internal static int Read(byte[] image, int[] heaps, int[] methods, int row, int first, int[] offsets)
        {
            var counts = new int[45];
            if (MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts) < 0) return -1;
            int extent = BackingBytes;
            if (counts[15] != 0)
            {
                int table = MetadataTableRows.TableStart(heaps, methods, counts, 15);
                if (table < 0) return -1;
                int index = counts[2] < 65536 ? 2 : 4;
                bool found = false;
                for (int i = 0; i < counts[15]; i++)
                {
                    uint at = (uint)(table + i * (6 + index));
                    if (ReadIndex(image, at + 6, index) != (uint)row) continue;
                    if (found) return -1;
                    found = true;
                    uint pack = U16(image, at);
                    uint size = U32(image, at + 2);
                    if (size > BackingBytes || pack > 128 || (pack & (pack - 1)) != 0) return -2;
                    if (size != 0) extent = (int)size;
                }
            }

            if (counts[16] == 0) return extent;
            int fields = MetadataTableRows.TableStart(heaps, methods, counts, 16);
            if (fields < 0) return -1;
            int fieldIndex = counts[4] < 65536 ? 2 : 4;
            for (int i = 0; i < counts[16]; i++)
            {
                uint at = (uint)(fields + i * (4 + fieldIndex));
                int field = (int)ReadIndex(image, at + 4, fieldIndex) - first;
                if (field < 0 || field >= offsets.Length) continue;
                if (offsets[field] != 0) return -1;
                uint offset = U32(image, at);
                if (offset >= BackingBytes) return -2;
                offsets[field] = (int)offset + 1;
            }
            return extent;
        }

        private static uint ReadIndex(byte[] image, uint at, int width) =>
            width == 2 ? U16(image, at) : U32(image, at);

        internal static int Width(int storage)
        {
            if (storage == NarrowStorage.Signed8 || storage == NarrowStorage.Unsigned8) return 1;
            if (storage >= NarrowStorage.Signed16 && storage <= NarrowStorage.Character) return 2;
            if (storage == 1 || storage == FloatStorage.Single) return 4;
            return storage == WideStorage.Scalar || storage == FloatStorage.Double ? 8 : 0;
        }
    }
}
