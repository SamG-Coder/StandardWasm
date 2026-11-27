using static PeLibrary.ImageBytes;

namespace PeLibrary
{
    internal static class StrongNames
    {
        internal static int ReadKey(byte[] image, int[] heaps, int index, int[] data)
        {
            if (index == 0) return 0;
            if (index < 0 || index >= heaps[1]) return -1;
            int end = heaps[0] + heaps[1];
            int length = CompressedUnsigned(image, heaps[0] + index, end, data);
            if (length <= 0 || length > end - data[0]) return -1;
            return length;
        }

        internal static bool Equal(byte[] source, int start, byte[] target, int other, int length)
        {
            for (int i = 0; i < length; i++)
                if (source[start + i] != target[other + i]) return false;
            return true;
        }

        // CLR metadata defines a token as the final eight SHA-1 digest bytes
        // in reverse order. The digest covers the complete stored public key.
        // This calculates identity only; it does not verify a signature.
        internal static bool MatchesToken(byte[] token, int start, byte[] key, int offset, int length) =>
            MatchesTokenWords(key, offset, length, U32(token, (uint)start), U32(token, (uint)start + 4));

        internal static bool MatchesTokenWords(byte[] key, int offset, int length, uint low, uint high) =>
            TokenWords(key, offset, length) == ((long)high << 32 | low);

        internal static long TokenWords(byte[] key, int offset, int length)
        {
            uint h0 = 0x67452301, h1 = 0xefcdab89, h2 = 0x98badcfe, h3 = 0x10325476, h4 = 0xc3d2e1f0;
            var words = new int[16];
            int padded = (length + 72) & ~63;
            // A compressed metadata length is at most 29 bits, so its bit
            // length fits the low 32 bits of the SHA-1 padding field.
            uint bits = (uint)length << 3;
            for (int block = 0; block < padded; block += 64)
            {
                for (int i = 0; i < 16; i++)
                {
                    uint word = 0;
                    for (int j = 0; j < 4; j++)
                    {
                        int position = block + i * 4 + j;
                        uint value = 0;
                        if (position < length) value = key[offset + position];
                        else if (position == length) value = 0x80;
                        else if (position >= padded - 4) value = (bits >> ((padded - 1 - position) * 8)) & 255;
                        word = (word << 8) | value;
                    }
                    words[i] = unchecked((int)word);
                }

                uint a = h0, b = h1, c = h2, d = h3, e = h4;
                for (int i = 0; i < 80; i++)
                {
                    int slot = i & 15;
                    if (i >= 16) words[slot] = unchecked((int)Rotate((uint)(words[(i - 3) & 15] ^ words[(i - 8) & 15] ^
                        words[(i - 14) & 15] ^ words[slot]), 1));
                    uint function, constant;
                    if (i < 20) { function = (b & c) | (~b & d); constant = 0x5a827999; }
                    else if (i < 40) { function = b ^ c ^ d; constant = 0x6ed9eba1; }
                    else if (i < 60) { function = (b & c) | (b & d) | (c & d); constant = 0x8f1bbcdc; }
                    else { function = b ^ c ^ d; constant = 0xca62c1d6; }
                    uint temp = unchecked(Rotate(a, 5) + function + e + constant + (uint)words[slot]);
                    e = d;
                    d = c;
                    c = Rotate(b, 30);
                    b = a;
                    a = temp;
                }
                h0 = unchecked(h0 + a);
                h1 = unchecked(h1 + b);
                h2 = unchecked(h2 + c);
                h3 = unchecked(h3 + d);
                h4 = unchecked(h4 + e);
            }

            return (long)h3 << 32 | h4;
        }

        private static uint Rotate(uint value, int count) => (value << count) | (value >> (32 - count));
    }
}
