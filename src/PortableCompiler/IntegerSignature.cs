namespace PortableCompiler
{
    internal static class IntegerSignature
    {
        internal static int ParameterCount(byte[] image, int[] heap, int index)
        {
            if (index <= 0 || index >= heap[1])
                return -1;
            int cursor = heap[0] + index;
            int end = heap[0] + heap[1];
            var next = new int[1];
            int size = CompressedUnsigned(image, cursor, end, next);
            cursor = next[0];
            if (size < 3 || size > end - cursor || image[cursor] != 0)
                return -1;

            // DEFAULT managed calling convention, no HASTHIS or GENERIC flag.
            end = cursor + size;
            int count = CompressedUnsigned(image, cursor + 1, end, next);
            cursor = next[0];
            if (count < 0 || end - cursor != count + 1 || image[cursor] != 8)
                return -1;
            for (int i = 0; i < count; i++)
                if (image[cursor + 1 + i] != 8)
                    return -1;
            return count;
        }

        private static int CompressedUnsigned(byte[] image, int cursor, int end, int[] next)
        {
            if (cursor >= end)
                return -1;
            int first = image[cursor++];
            if (first < 128)
            {
                next[0] = cursor;
                return first;
            }
            if (first < 192 && end - cursor >= 1)
            {
                next[0] = cursor + 1;
                return ((first & 63) << 8) | image[cursor];
            }
            if (first < 224 && end - cursor >= 3)
            {
                next[0] = cursor + 3;
                return ((first & 31) << 24) | (image[cursor] << 16) |
                    (image[cursor + 1] << 8) | image[cursor + 2];
            }
            return -1;
        }
    }
}
