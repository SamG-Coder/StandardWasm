using static PeLibrary.ImageBytes;

namespace PeLibrary
{
    internal static class VirtualSlotMetadata
    {
        internal static int ReadMethodRange(byte[] image, int[] heaps, int[] layout, int type, int[] range)
        {
            var counts = new int[45];
            if (MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts) < 0 ||
                type < 1 || type > counts[2]) return -1;
            int strings = layout[2];
            int fields = counts[4] < 65536 ? 2 : 4;
            int methods = counts[6] < 65536 ? 2 : 4;
            int extends = counts[2] < 16384 && counts[1] < 16384 && counts[27] < 16384 ? 2 : 4;
            int methodColumn = 4 + 2 * strings + extends + fields;
            int width = methodColumn + methods;
            // CompressedTables validated the TypeDef and Field tables.
            int table = layout[0] - counts[4] * (2 + strings + layout[3]) - counts[2] * width;
            uint at = (uint)(table + (type - 1) * width + methodColumn);
            int first = Index(image, at, methods);
            int end = type == counts[2] ? counts[6] + 1 : Index(image, at + (uint)width, methods);
            if (first < 1 || end < first || end > counts[6] + 1) return -1;
            range[0] = first;
            range[1] = end;
            return 0;
        }

        // An explicit override can bind the referenced slot to a differently
        // named body. Reject any MethodImpl declaration with this slot name.
        // Unrelated declarations cannot map the named class slot.
        internal static int HasNamedMethodImpl(byte[] image, int[] heaps, int[] layout, int type,
            byte[] declaration, int[] declarationHeaps, int slotName)
        {
            var counts = new int[45];
            if (MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts) < 0) return -1;
            if (counts[25] == 0) return 0;
            int table = MetadataTableRows.TableStart(heaps, layout, counts, 25);
            if (table < 0) return -1;
            int classWidth = counts[2] < 65536 ? 2 : 4;
            int methodWidth = MetadataTableRows.MethodRefWidth(counts);
            int width = classWidth + 2 * methodWidth;
            int memberTable = counts[10] == 0 ? 0 : MetadataTableRows.TableStart(heaps, layout, counts, 10);
            int strings = layout[2], blobs = layout[3];
            int memberWidth = StandaloneSignatures.Width(counts, 10, strings, blobs);
            for (int i = 0; i < counts[25]; i++)
            {
                uint at = (uint)(table + i * width);
                if (Index(image, at, classWidth) != type) continue;
                int token = Index(image, at + (uint)(classWidth + methodWidth), methodWidth);
                int row = token >> 1;
                if (row < 1) return -1;
                int name;
                if ((token & 1) == 0)
                {
                    if (row > counts[6]) return -1;
                    name = MethodDefinitions.ReadColumn(image, layout, row, 3);
                }
                else
                {
                    if (row > counts[10] || memberTable < 0) return -1;
                    uint nameAt = (uint)(memberTable + row * memberWidth - strings - blobs);
                    name = Index(image, nameAt, strings);
                }
                if (AssemblyReferences.SameString(declaration, declarationHeaps, slotName, image, heaps, name))
                    return 1;
            }
            return 0;
        }

        private static int Index(byte[] image, uint offset, int width) =>
            width == 2 ? (int)U16(image, offset) : unchecked((int)U32(image, offset));
    }
}
