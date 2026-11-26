namespace PeLibrary
{
    internal static class PointerStorage
    {
        internal const byte NullPointer = NativeStorage.Scalar + 1;
        internal const byte NativeZero = NullPointer + 1;
        internal const int Element = 0x0f;

        // Consume the pointee without constructing its layout. A positive
        // result is the image-local TypeDefOrRef for a named value type.
        internal static int ReadPointee(byte[] image, int position, int end, int[] next)
        {
            while (position < end && image[position] == Element) position++;
            if (position >= end) return -1;
            int element = image[position++];
            next[0] = position;
            if (element == 0x11)
            {
                int coded = ImageBytes.CompressedUnsigned(image, position, end, next);
                return coded > 0 && (coded & 3) <= 1 && coded >> 2 > 0 ? coded : -1;
            }
            return element >= 1 && element <= 0x0d || NativeStorage.IsIntegerElement(element) ? 0 : -2;
        }
    }
}
