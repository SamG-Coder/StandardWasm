using static PeLibrary.ImageBytes;

namespace PeLibrary
{
    internal static class AssemblyAttributeRecords
    {
        internal static int Find(byte[] image, int[] layout, int[] heaps, int[] counts, int[] table)
        {
            if (MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts) < 0 || counts[32] != 1) return -1;
            int count = counts[12];
            if (count == 0) return 0;
            int start = MetadataTableRows.TableStart(heaps, layout, counts, 12);
            if (start < 0) return start;
            table[0] = start;
            table[1] = StandaloneSignatures.Width(counts, 12, layout[2], layout[3]);
            table[2] = StandaloneSignatures.HasCustomAttribute(counts);
            return count;
        }

        internal static bool Read(byte[] image, int[] layout, int[] table, int row, int[] entry)
        {
            uint start = (uint)(table[0] + row * table[1]);
            const int assembly = (1 << 5) | 14; // AssemblyDef row 1 in HasCustomAttribute.
            if (AttributeConstructors.ReadRowIndex(image, start, table[2]) != assembly) return false;
            entry[0] = AttributeConstructors.ReadRowIndex(image, start + (uint)table[2], table[1] - table[2] - layout[3]);
            entry[1] = AttributeConstructors.ReadRowIndex(image, start + (uint)(table[1] - layout[3]), layout[3]);
            return true;
        }

        internal static bool IsStringPairConstructor(byte[] image, int[] heaps, int[] layout, int constructor)
        {
            int kind = constructor & 7, row = constructor >> 3;
            uint name;
            int blob;
            if (kind == 2)
            {
                name = (uint)MethodDefinitions.ReadColumn(image, layout, row, 3);
                blob = MethodDefinitions.ReadColumn(image, layout, row, 4);
            }
            else if (kind == 3)
            {
                var member = new int[AssemblyReferences.SymbolWidth];
                if (AssemblyReferences.ReadMember(image, heaps, layout, row, member) < 0) return false;
                name = (uint)member[4];
                blob = member[5];
            }
            else return false;
            if (!MatchesAscii(image, heaps, name, ".ctor")) return false;
            var cursor = new int[1];
            int end = SignatureReader.ReadInstance(image, heaps, blob, 2, cursor);
            return end >= 0 && SignatureReader.TakeElement(image, cursor, end, SignatureReader.Void) &&
                SignatureReader.TakeElement(image, cursor, end, 0x0e) &&
                SignatureReader.TakeElement(image, cursor, end, 0x0e) && cursor[0] == end;
        }

        internal static int ReadStringPair(byte[] image, int[] heaps, int blob, byte[] literals, int[] size, int[] pair)
        {
            if (blob <= 0 || blob >= heaps[1]) return -1;
            var cursor = new int[1];
            int limit = heaps[0] + heaps[1];
            int bytes = CompressedUnsigned(image, heaps[0] + blob, limit, cursor);
            int start = cursor[0];
            if (bytes < 6 || bytes > limit - start || U16(image, (uint)start) != 1) return -1;
            int end = start + bytes;
            cursor[0] += 2;
            int status = ReadSerString(image, cursor, end, literals, size, pair, 0);
            if (status < 0) return status;
            status = ReadSerString(image, cursor, end, literals, size, pair, 2);
            if (status < 0) return status;
            // AssemblyMetadataAttribute only declares the two constructor arguments.
            return end - cursor[0] == 2 && U16(image, (uint)cursor[0]) == 0 ? 0 : -2;
        }

        private static int ReadSerString(byte[] image, int[] cursor, int end, byte[] literals, int[] size, int[] pair, int at)
        {
            if (cursor[0] >= end) return -1;
            if (image[cursor[0]] == 0xff)
            {
                pair[at] = -1;
                pair[at + 1] = 0;
                cursor[0]++;
                return 0;
            }
            int bytes = CompressedUnsigned(image, cursor[0], end, cursor);
            if (bytes < 0 || bytes > end - cursor[0]) return -1;
            int offset = size[0];
            int copied = Utf8Scalars.CopyUtf16(image, cursor[0], bytes, literals, offset);
            if (copied < 0) return copied;
            cursor[0] += bytes;
            size[0] += copied;
            pair[at] = offset;
            pair[at + 1] = copied / 2;
            return 0;
        }
    }
}
