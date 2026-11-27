using static PeLibrary.ImageBytes;

namespace PeLibrary
{
    internal static class EnumLiteralMetadata
    {
        // The nominal enum has already been resolved. Preserve metadata order
        // until the caller has determined whether alias selection needs sorting.
        internal static int Read(byte[] image, int[] heaps, int[] methods, int row,
            int[] shape, ulong[] values, int[] names, int[] fields)
        {
            var counts = new int[45];
            int first = MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts);
            if (first < 0) return first;
            int flags = FlagsAttribute(image, heaps, methods, counts, first, row);
            if (flags != 0) return flags < 0 ? flags : -2;

            int start = shape[1], end = start + shape[2];
            int valueField = 0, element = 0;
            var field = new int[3];
            for (int r = start; r < end; r++)
            {
                AssemblyReferences.ReadField(image, methods, shape[0], r, field);
                if ((field[0] & 0x10) == 0)
                {
                    if (valueField != 0 || field[2] < 1 || !Fits((uint)field[2], 3, (uint)heaps[1])) return -1;
                    valueField = r;
                    element = image[heaps[0] + field[2] + 2];
                    continue;
                }
                if (field[1] <= 0 || field[1] >= heaps[3]) return -1;
                names[r - start] = field[1];
                fields[r - start] = r;
            }
            if (valueField == 0 || element < 4 || element > 11) return -2;
            int width = 1 << ((element - 4) / 2);
            if (end - start == 1) return element;

            int table = MetadataTableRows.TableStart(heaps, methods, counts, 11);
            if (table < 0) return -1;
            int constantWidth = StandaloneSignatures.Width(counts, 11, methods[2], methods[3]);
            int parentWidth = constantWidth - 2 - methods[3];
            var next = new int[1];
            int blobEnd = heaps[0] + heaps[1], remaining = end - start - 1;
            for (int i = 0; i < counts[11] && remaining > 0; i++)
            {
                uint at = (uint)(table + i * constantWidth);
                int parent = AttributeConstructors.ReadRowIndex(image, at + 2, parentWidth);
                int fieldRow = parent >> 2;
                if ((parent & 3) != 0 || fieldRow < start || fieldRow >= end) continue;
                int index = fieldRow - start;
                if (fields[index] != fieldRow || names[index] <= 0 || U16(image, at) != element) return -1;
                int blob = AttributeConstructors.ReadRowIndex(image, at + (uint)(2 + parentWidth), methods[3]);
                if (blob < 1 || blob >= heaps[1] || CompressedUnsigned(image, heaps[0] + blob, blobEnd, next) != width ||
                    width > blobEnd - next[0]) return -1;
                uint value = (uint)next[0];
                values[index] = width == 1 ? image[value] : width == 2 ? U16(image, value) :
                    width == 4 ? U32(image, value) : (ulong)U32(image, value) | (ulong)U32(image, value + 4) << 32;
                names[index] = -names[index];
                remaining--;
            }
            return remaining == 0 ? element : -1;
        }

        internal static int CopyName(byte[] image, int[] heaps, int name, byte[] data, int offset)
        {
            int start = heaps[2] + name, end = start;
            int limit = heaps[2] + heaps[3];
            while (end < limit && image[end] != 0) end++;
            return end == start || end == limit ? -1 : Utf8Scalars.CopyUtf16(image, start, end - start, data, offset);
        }

        private static int FlagsAttribute(byte[] image, int[] heaps, int[] methods, int[] counts, int first, int row)
        {
            if (counts[12] == 0) return 0;
            int table = MetadataTableRows.TableStart(heaps, methods, counts, 12);
            if (table < 0) return -1;
            int parentWidth = StandaloneSignatures.HasCustomAttribute(counts);
            int width = StandaloneSignatures.Width(counts, 12, methods[2], methods[3]);
            int constructorWidth = width - parentWidth - methods[3];
            int parent = row << 5 | 3;
            for (int index = 0; index < counts[12]; index++)
            {
                uint at = (uint)(table + index * width);
                if (AttributeConstructors.ReadRowIndex(image, at, parentWidth) != parent) continue;
                int constructor = AttributeConstructors.ReadRowIndex(image, at + (uint)parentWidth, constructorWidth);
                int type = AttributeConstructors.ReadType(image, heaps, methods, counts, constructor);
                if (type < 0) return type;
                if (type == 0) continue;
                int owner = type >> 3;
                if ((type & 7) == 0)
                {
                    if (ObjectLayouts.NamedDefinition(image, heaps, methods, counts, first, owner, "System", "FlagsAttribute") &&
                        PlatformAssemblyIdentity.IsCoreLibDefinition(image, heaps, methods, counts)) return 1;
                }
                else
                {
                    var reference = new int[AssemblyReferences.SymbolWidth];
                    if (AssemblyReferences.ReadType(image, heaps, methods, owner, reference) >= 0 &&
                        reference[AssemblyReferences.Depth] == 0 &&
                        MatchesAscii(image, heaps, (uint)reference[2], "FlagsAttribute") &&
                        MatchesAscii(image, heaps, (uint)reference[3], "System") &&
                        PlatformAssemblyIdentity.IsPlatformAssembly(image, heaps, methods, counts,
                            reference[AssemblyReferences.AssemblyRow], includeRuntimeExtensions: false)) return 1;
                }
            }
            return 0;
        }
    }
}
