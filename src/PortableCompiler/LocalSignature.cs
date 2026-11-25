namespace PortableCompiler
{
    internal static class LocalSignature
    {
        internal static int Count(byte[] image, int[] heap, int index)
        {
            if (index <= 0 || index >= heap[1])
                return -1;
            var next = new int[1];
            int end = heap[0] + heap[1];
            int size = IntegerSignature.CompressedUnsigned(image, heap[0] + index, end, next);
            int cursor = next[0];
            if (size < 2 || size > end - cursor || image[cursor] != 7)
                return -1;
            end = cursor + size;
            int count = IntegerSignature.CompressedUnsigned(image, cursor + 1, end, next);
            cursor = next[0];
            if (count < 0 || end - cursor != count)
                return -1;
            for (int i = 0; i < count; i++)
                if (image[cursor + i] != 8)
                    return -1;
            return count;
        }
    }
}
