namespace PeLibrary
{
    internal static class Utf8Scalars
    {
        // Read one Unicode scalar. Reject overlong encodings, surrogates,
        // truncated input and values above U+10FFFF.
        internal static int Read(byte[] bytes, int start, int end, int[] next)
        {
            if (start >= end) return -1;
            int first = bytes[start++];
            if (first < 128)
            {
                next[0] = start;
                return first;
            }
            if (first < 194 || first > 244) return -1;
            int remaining = first < 224 ? 1 : first < 240 ? 2 : 3;
            if (remaining > end - start) return -1;
            int low = first == 224 ? 160 : first == 240 ? 144 : 128;
            int high = first == 237 ? 159 : first == 244 ? 143 : 191;
            int second = bytes[start++];
            if (second < low || second > high) return -1;
            int value = (first & (127 >> (remaining + 1))) << 6 | (second & 63);
            for (int index = 1; index < remaining; index++)
            {
                int part = bytes[start++];
                if (part < 128 || part > 191) return -1;
                value = value << 6 | (part & 63);
            }
            next[0] = start;
            return value;
        }

        // Validate and size the source before modifying the separate data buffer.
        internal static int CopyUtf16(byte[] image, int start, int length, byte[] output, int offset)
        {
            var next = new int[1];
            int end = start + length, cursor = start, bytes = 0;
            while (cursor < end)
            {
                int scalar = Read(image, cursor, end, next);
                if (scalar < 0) return -1;
                int width = scalar < 65536 ? 2 : 4;
                if (bytes > int.MaxValue - width) return -3;
                bytes += width;
                cursor = next[0];
            }
            if (offset < 0 || offset > output.Length || bytes > output.Length - offset) return -3;
            cursor = start;
            while (cursor < end)
            {
                int scalar = Read(image, cursor, end, next);
                cursor = next[0];
                if (scalar >= 65536)
                {
                    scalar -= 65536;
                    offset = WriteUnit(output, offset, 0xd800 | (scalar >> 10));
                    scalar = 0xdc00 | (scalar & 1023);
                }
                offset = WriteUnit(output, offset, scalar);
            }
            return bytes;
        }

        private static int WriteUnit(byte[] output, int offset, int unit)
        {
            output[offset] = unchecked((byte)unit);
            output[offset + 1] = (byte)(unit >> 8);
            return offset + 2;
        }
    }
}
