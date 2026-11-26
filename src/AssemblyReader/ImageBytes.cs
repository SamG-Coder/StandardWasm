namespace PeLibrary
{
    internal static class ImageBytes
    {
        internal static bool Fits(uint offset, uint length, uint limit) =>
            offset <= limit && length <= limit - offset;

        internal static uint U16(byte[] bytes, uint offset) =>
            (uint)(bytes[offset] | (bytes[offset + 1] << 8));

        internal static uint U32(byte[] bytes, uint offset) =>
            U16(bytes, offset) | (U16(bytes, offset + 2) << 16);

        internal static int CompressedUnsigned(byte[] image, int cursor, int end, int[] next)
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
