using static PeLibrary.ImageBytes;

namespace PeLibrary
{
    internal static class StandaloneSignatures
    {
        internal static int BlobIndex(byte[] image, int[] streams, int[] methods, int methodCount, int token)
        {
            int row = token & 0x00ffffff;
            if ((uint)token >> 24 != 0x11 || row == 0)
                return -1;
            int start = streams[4], size = streams[5];
            var counts = new int[45];
            if (MetadataTableRows.ReadCounts(image, start, size, counts) < 0 || row > counts[17])
                return -1;
            int strings = methods[2], blobs = methods[3];
            int cursor = methods[0] + methodCount * methods[1];
            int end = start + size;
            for (int table = 8; table < 17; table++)
            {
                if (counts[table] == 0)
                    continue;
                int width = Width(counts, table, strings, blobs);
                if (counts[table] > (end - cursor) / width)
                    return -1;
                cursor += counts[table] * width;
            }
            if (counts[17] > (end - cursor) / blobs)
                return -1;
            uint offset = (uint)(cursor + (row - 1) * blobs);
            return blobs == 2 ? (int)U16(image, offset) : unchecked((int)U32(image, offset));
        }

        internal static int Width(int[] counts, int table, int strings, int blobs)
        {
            if (table == 8) return 4 + strings; // Param
            if (table == 9) return Index(counts[2]) + Coded(2, counts[2], counts[1], counts[27]); // InterfaceImpl
            if (table == 10) // MemberRefParent: TypeDef, TypeRef, ModuleRef, MethodDef, TypeSpec
            {
                int parent = Coded(3, counts[2], counts[1], counts[26]);
                if (Coded(3, counts[6], counts[27], 0) == 4)
                    parent = 4;
                return parent + strings + blobs;
            }
            if (table == 11) return 2 + Coded(2, counts[4], counts[8], counts[23]) + blobs; // Constant
            if (table == 12) return HasCustomAttribute(counts) + Coded(3, counts[6], counts[10], 0) + blobs;
            if (table == 13) return Coded(1, counts[4], counts[8], 0) + blobs; // FieldMarshal
            if (table == 14) return 2 + Coded(2, counts[2], counts[6], counts[32]) + blobs; // DeclSecurity
            if (table == 15) return 6 + Index(counts[2]); // ClassLayout
            return 4 + Index(counts[4]); // FieldLayout
        }

        internal static int HasCustomAttribute(int[] counts)
        {
            // ECMA-335 II.24.2.6 HasCustomAttribute targets, five tag bits.
            for (int i = 0; i < counts.Length; i++)
            {
                if (counts[i] < 2048)
                    continue;
                if (i == 6 || i == 4 || i == 1 || i == 2 || i == 8 || i == 9 || i == 10 ||
                    i == 0 || i == 14 || i == 23 || i == 20 || i == 17 || i == 26 || i == 27 ||
                    i == 32 || i == 35 || i == 38 || i == 39 || i == 40 || i == 42 || i == 44 || i == 43)
                    return 4;
            }
            return 2;
        }

        private static int Index(int count) => count < 65536 ? 2 : 4;

        private static int Coded(int tags, int a, int b, int c)
        {
            int limit = 65536 >> tags;
            return a < limit && b < limit && c < limit ? 2 : 4;
        }
    }
}
