using static PeLibrary.ImageBytes;

namespace PeLibrary
{
    internal static class StaticFieldMetadata
    {
        internal static int Check(byte[] image, int[] heaps, int[] layout, int row, int flags, bool write)
        {
            const int Static = 0x10, InitOnly = 0x20, Literal = 0x40, HasRva = 0x100,
                HasMarshal = 0x1000, HasDefault = 0x8000;
            if ((flags & Static) == 0 || (flags & (Literal | HasRva | HasMarshal | HasDefault)) != 0 ||
                (write && (flags & InitOnly) != 0)) return -2;

            // Field attributes can change storage lifetime, including ThreadStatic.
            // Defer attributed statics until the runtime can distinguish their storage.
            var counts = new int[45];
            if (MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts) < 0) return -1;
            if (counts[12] == 0) return 0;
            int table = MetadataTableRows.TableStart(heaps, layout, counts, 12);
            if (table < 0) return -1;
            int parentWidth = StandaloneSignatures.HasCustomAttribute(counts);
            int width = StandaloneSignatures.Width(counts, 12, layout[2], layout[3]);
            int parent = row << 5 | 1; // HasCustomAttribute.FieldDef.
            for (int i = 0; i < counts[12]; i++)
            {
                uint at = (uint)(table + i * width);
                int candidate = parentWidth == 2 ? (int)U16(image, at) : unchecked((int)U32(image, at));
                if (candidate == parent) return -2;
            }
            return 0;
        }
    }
}
