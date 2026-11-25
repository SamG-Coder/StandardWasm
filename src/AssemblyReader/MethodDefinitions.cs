using static PeLibrary.ImageBytes;

namespace PeLibrary
{
    internal static class MethodDefinitions
    {
        internal const int Malformed = -1, Unsupported = -2, Missing = -3;

        // layout = absolute row start, row width, String index width, Blob index width.
        // Returns the row count on success and leaves layout unchanged on error.
        internal static int ReadLayout(byte[] image, int start, int size, int[] layout)
        {
            if (layout.Length < 4)
                return Malformed;
            var counts = new int[45];
            int first = MetadataTableRows.ReadCounts(image, start, size, counts);
            if (first < 0)
                return first;
            uint cursor = (uint)first;
            uint end = (uint)(start + size);
            uint heaps = image[start + 6];

            int strings = HeapWidth(heaps, 1);
            int guids = HeapWidth(heaps, 2);
            int blobs = HeapWidth(heaps, 4);
            int resolutionScope = CodedWidth(counts[0], counts[26], counts[35], counts[1]);
            int typeDefOrRef = CodedWidth(counts[2], counts[1], counts[27], 0);
            // Physical row order in a compressed stream starts Module, TypeRef,
            // TypeDef, Field and MethodDef, with pointer tables excluded above.
            if (!Skip(counts[0], 2 + strings + 3 * guids, end, cursor))
                return Malformed;
            cursor += (uint)(counts[0] * (2 + strings + 3 * guids));
            int typeRefWidth = resolutionScope + 2 * strings;
            if (!Skip(counts[1], typeRefWidth, end, cursor))
                return Malformed;
            cursor += (uint)(counts[1] * typeRefWidth);
            int typeDefWidth = 4 + 2 * strings + typeDefOrRef + IndexWidth(counts[4]) + IndexWidth(counts[6]);
            if (!Skip(counts[2], typeDefWidth, end, cursor))
                return Malformed;
            cursor += (uint)(counts[2] * typeDefWidth);
            int fieldWidth = 2 + strings + blobs;
            if (!Skip(counts[4], fieldWidth, end, cursor))
                return Malformed;
            cursor += (uint)(counts[4] * fieldWidth);
            int methodWidth = 8 + strings + blobs + IndexWidth(counts[8]);
            if (!Skip(counts[6], methodWidth, end, cursor))
                return Malformed;

            layout[0] = (int)cursor;
            layout[1] = methodWidth;
            layout[2] = strings;
            layout[3] = blobs;
            return counts[6];
        }

        // The layout and row must come from the validated prefix of this image.
        internal static int ReadColumn(byte[] image, int[] layout, int row, int field)
        {
            uint start = (uint)(layout[0] + (row - 1) * layout[1]);
            if (field == 0) return (int)U32(image, start);
            if (field == 1) return (int)U16(image, start + 4);
            if (field == 2) return (int)U16(image, start + 6);
            if (field == 3) return ReadIndex(image, start + 8, layout[2]);
            if (field == 4) return ReadIndex(image, start + 8 + (uint)layout[2], layout[3]);
            return Malformed;
        }

        private static int ReadIndex(byte[] image, uint offset, int width) =>
            width == 2 ? (int)U16(image, offset) : unchecked((int)U32(image, offset));

        private static bool Skip(int count, int width, uint end, uint cursor) =>
            (uint)count <= (end - cursor) / (uint)width;

        private static int HeapWidth(uint flags, uint flag) => (flags & flag) == 0 ? 2 : 4;
        private static int IndexWidth(int count) => count < 65536 ? 2 : 4;

        // Both coded indexes used before MethodDef reserve two tag bits.
        private static int CodedWidth(int a, int b, int c, int d) =>
            a < 16384 && b < 16384 && c < 16384 && d < 16384 ? 2 : 4;
    }
}
