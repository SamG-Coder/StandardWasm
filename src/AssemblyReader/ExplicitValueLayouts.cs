using static PeLibrary.ImageBytes;

namespace PeLibrary
{
    internal static class ExplicitValueLayouts
    {
        // The current managed backing cell covers eight bytes. A zero
        // ClassLayout size leaves the extent to the declared field offsets.
        private const int BackingBytes = 8;
        private const int MaxArrayElementSize = ushort.MaxValue; // CoreCLR value-type array element limit.

        // Positive for a fieldless explicit value with a supported declared size.
        // Its nominal size is observable, although a managed copy has no fields.
        internal static int ReadFieldlessSize(byte[] image, int[] heaps, int[] methods, int row,
            int table, int first, int fields)
        {
            int fieldWidth = 2 + methods[2] + methods[3];
            for (int field = 0; field < fields; field++)
                if ((U16(image, (uint)(table + (first + field - 1) * fieldWidth)) & 0x10) == 0) return 0;

            var counts = new int[45];
            if (MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts) < 0) return -1;
            var declared = new uint[2];
            int read = ReadClassLayout(image, heaps, methods, counts, row, declared);
            if (read < 0) return read;
            return declared[0] == 0 && declared[1] <= MaxArrayElementSize ? (int)declared[1] : 0;
        }

        internal static int Read(byte[] image, int[] heaps, int[] methods, int row, int first, int[] offsets,
            bool wide = false)
        {
            var counts = new int[45];
            if (MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts) < 0) return -1;
            var declared = new uint[2];
            int layout = ReadClassLayout(image, heaps, methods, counts, row, declared);
            if (layout < 0) return layout;
            uint pack = declared[0], size = declared[1];
            if ((wide ? size != 0 : size > BackingBytes) ||
                pack > 128 || (pack & (pack - 1)) != 0) return -2;
            int extent = size == 0 ? BackingBytes : (int)size;

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
                if (offset >= (wide ? int.MaxValue - 8u : BackingBytes)) return -2;
                offsets[field] = (int)offset + 1;
            }
            return extent;
        }

        private static int ReadClassLayout(byte[] image, int[] heaps, int[] methods, int[] counts, int row, uint[] declared)
        {
            if (counts[15] == 0) return 0;
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
                declared[0] = U16(image, at);
                declared[1] = U32(image, at + 2);
            }
            return 0;
        }

        private static uint ReadIndex(byte[] image, uint at, int width) =>
            width == 2 ? U16(image, at) : U32(image, at);

        internal static int ViewWidth(int[] objects, int storage)
        {
            if (!ObjectLayouts.IsValueType(objects, storage)) return Width(storage);
            if (ObjectLayouts.IsExplicitValue(objects, storage)) return 0;

            // The aggregate contains only CLR fields. Padding cannot be
            // reconstructed after a scalar write supplies bits for those bytes.
            // CoreCLR groups auto-layout scalars by size and preserves the
            // declared order within one size group.
            bool auto = ObjectLayouts.IsAutoValue(objects, storage);
            int extent = 0, alignment = 1;
            for (int i = 0; i < ObjectLayouts.FieldCount(objects, storage); i++)
            {
                int field = ObjectLayouts.FieldTypeAt(objects, storage, i);
                if (field == 0) continue;
                int width = Width(field);
                if (width == 0 || extent + width > BackingBytes || extent % width != 0 ||
                    (auto && extent != 0 && width != alignment)) return 0;
                if (width > alignment) alignment = width;
                extent += width;
            }
            // CoreCLR rounds small auto-layout value types up to a power of
            // two. Copies can preserve the trailing bytes of that CLR layout.
            if (auto && (extent & (extent - 1)) != 0) return 0;
            return extent % alignment == 0 ? extent : 0;
        }

        internal static int Width(int storage)
        {
            if (storage == NarrowStorage.Signed8 || storage == NarrowStorage.Unsigned8) return 1;
            if (storage >= NarrowStorage.Signed16 && storage <= NarrowStorage.Character) return 2;
            if (storage == 1 || storage == FloatStorage.Single) return 4;
            return storage == WideStorage.Scalar || storage == FloatStorage.Double ? 8 : 0;
        }
    }
}
