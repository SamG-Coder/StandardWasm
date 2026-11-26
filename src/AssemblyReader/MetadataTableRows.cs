using static PeLibrary.ImageBytes;

namespace PeLibrary
{
    internal static class MetadataTableRows
    {
        internal static int ReadCounts(byte[] image, int start, int size, int[] counts)
        {
            if (start < 0 || size < 24 || !Fits((uint)start, (uint)size, (uint)image.Length))
                return MethodDefinitions.Malformed;
            uint header = (uint)start;
            uint end = (uint)(start + size);
            uint heaps = image[header + 6];
            uint validLow = U32(image, header + 8);
            uint validHigh = U32(image, header + 12);
            // This reader handles ordinary ECMA compressed tables. Pointer and ENC
            // tables require different ownership rules. PDB tables use other layouts.
            const uint unsupportedTables = (1u << 3) | (1u << 5) | (1u << 7) |
                (1u << 19) | (1u << 22) | (1u << 30) | (1u << 31);
            if (image[header + 4] != 2 || (heaps & ~7u) != 0 ||
                (validLow & unsupportedTables) != 0 || (validHigh & ~0x1fffu) != 0)
                return MethodDefinitions.Unsupported;
            uint cursor = header + 24;
            for (int table = 0; table < counts.Length; table++)
            {
                uint mask = table < 32 ? validLow : validHigh;
                if ((mask & (1u << (table & 31))) == 0)
                    continue;
                if (!Fits(cursor, 4, end))
                    return MethodDefinitions.Malformed;
                uint count = U32(image, cursor);
                if (count > 0xffffff)
                    return MethodDefinitions.Malformed;
                counts[table] = (int)count;
                cursor += 4;
            }

            return (int)cursor;
        }

        internal static int TableStart(int[] streams, int[] methods, int[] counts, int table)
        {
            int cursor = methods[0] + counts[6] * methods[1];
            int end = streams[4] + streams[5];
            for (int i = 8; i <= table; i++)
            {
                if (counts[i] == 0) continue;
                int width;
                if (i < 17) width = StandaloneSignatures.Width(counts, i, methods[2], methods[3]);
                else width = LaterWidth(counts, i, methods[2], methods[3]);
                if (width == 0 || counts[i] > (end - cursor) / width) return -1;
                if (i == table) return cursor;
                cursor += counts[i] * width;
            }
            return -1;
        }

        private static int LaterWidth(int[] counts, int table, int strings, int blobs)
        {
            if (table == 17 || table == 27) return blobs;
            if (table == 18) return Width(counts[2]) + Width(counts[20]);
            if (table == 20) return 2 + strings + (counts[2] < 16384 && counts[1] < 16384 && counts[27] < 16384 ? 2 : 4);
            if (table == 21) return Width(counts[2]) + Width(counts[23]);
            if (table == 23) return 2 + strings + blobs;
            if (table == 24) return 2 + Width(counts[6]) + (counts[20] < 32768 && counts[23] < 32768 ? 2 : 4);
            if (table == 25) return Width(counts[2]) + 2 * (counts[6] < 32768 && counts[10] < 32768 ? 2 : 4);
            if (table == 26) return strings;
            if (table == 28) return 2 + (counts[4] < 32768 && counts[6] < 32768 ? 2 : 4) + strings + Width(counts[26]);
            if (table == 29) return 4 + Width(counts[4]);
            if (table == 32) return 16 + blobs + 2 * strings;
            if (table == 33) return 4;
            if (table == 34) return 12;
            if (table == 35) return 12 + 2 * blobs + 2 * strings;
            if (table == 36) return 4 + Width(counts[35]);
            if (table == 37) return 12 + Width(counts[35]);
            if (table == 38) return 4 + strings + blobs;
            if (table == 39 || table == 40)
            {
                int implementation = counts[38] < 16384 && counts[35] < 16384 && counts[39] < 16384 ? 2 : 4;
                return 8 + (table == 39 ? 2 * strings : strings) + implementation;
            }
            if (table == 41) return 2 * Width(counts[2]);
            if (table == 42) return 4 + strings + (counts[2] < 32768 && counts[6] < 32768 ? 2 : 4);
            return table == 43 ? blobs + MethodRefWidth(counts) : 0;
        }

        internal static int MethodRefWidth(int[] counts) => counts[6] < 32768 && counts[10] < 32768 ? 2 : 4;

        private static int Width(int count) => count < 65536 ? 2 : 4;
    }
}
