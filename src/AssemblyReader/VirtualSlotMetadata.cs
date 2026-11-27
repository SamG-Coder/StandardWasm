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

        // Return the TypeDefOrRef of the next MethodImpl declaration with
        // this name. Zero ends the scan; an unmodeled parent returns -2.
        internal static int NextNamedMethodImplParent(byte[] image, int[] heaps, int[] layout, int type,
            byte[] declaration, int[] declarationHeaps, int slotName, int[] cursor, int[] implementation = null)
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
            for (int i = cursor[0]; i < counts[25]; i++)
            {
                uint at = (uint)(table + i * width);
                if (Index(image, at, classWidth) != type) continue;
                int token = Index(image, at + (uint)(classWidth + methodWidth), methodWidth);
                int row = token >> 1;
                if (row < 1) return -1;
                int name, signature, parent = 0;
                if ((token & 1) == 0)
                {
                    if (row > counts[6]) return -1;
                    name = MethodDefinitions.ReadColumn(image, layout, row, 3);
                    signature = MethodDefinitions.ReadColumn(image, layout, row, 4);
                }
                else
                {
                    if (row > counts[10] || memberTable < 0) return -1;
                    int parentWidth = memberWidth - strings - blobs;
                    uint member = (uint)(memberTable + (row - 1) * memberWidth);
                    parent = Index(image, member, parentWidth);
                    name = Index(image, member + (uint)parentWidth, strings);
                    signature = Index(image, member + (uint)(parentWidth + strings), blobs);
                }
                if (!AssemblyReferences.SameString(declaration, declarationHeaps, slotName, image, heaps, name))
                    continue;

                cursor[0] = i + 1;
                if (implementation != null)
                {
                    implementation[0] = Index(image, at + (uint)classWidth, methodWidth);
                    implementation[1] = signature;
                }
                if ((token & 1) == 0)
                {
                    int owner = ObjectLayouts.Owner(image, heaps, layout, row, false);
                    return owner > 0 ? owner << 2 : -1;
                }

                // TypeDef and TypeRef use the same first two tags in
                // MemberRefParent and TypeDefOrRef, with different widths.
                int parentTag = parent & 7;
                if (parentTag > 1 && parentTag != 4) return -2;
                int parentRow = parent >> 3;
                if (parentRow < 1 || parentRow > counts[parentTag == 4 ? 27 : parentTag == 0 ? 2 : 1]) return -1;
                return (parentRow << 2) | (parentTag == 4 ? 2 : parentTag);
            }
            return 0;
        }

        private static int Index(byte[] image, uint offset, int width) =>
            width == 2 ? (int)U16(image, offset) : unchecked((int)U32(image, offset));
    }
}
