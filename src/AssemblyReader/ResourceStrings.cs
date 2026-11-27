namespace PeLibrary
{
    // Issue #304: string lookup in a .resources stream (the binary format System.Resources
    // .ResourceReader reads; version 2). Used for CoreLib's neutral
    // System.Private.CoreLib.Strings.resources, so SR.GetResourceString("key") can be folded at
    // compile time. Layout (all integers little-endian Int32 unless noted):
    //   magic 0xBEEFCACE, header version, skip count, then [skip count] bytes (reader and set type names)
    //   version (2), resource count, type count, type names (7-bit length + UTF-8), padding to 8
    //   name hashes [count], name positions [count], data section offset
    //   name section: per entry 7-bit byte length, UTF-16LE name, Int32 data offset
    //   data section: per value 7-bit type code; String (1) is 7-bit byte length + UTF-8
    internal static class ResourceStrings
    {
        private const int StringTypeCode = 1;

        // Finds key (UTF-16 units in keyUnits[0 .. keyLength)) in the resources at
        // image[start .. start + length). Writes the value's UTF-16 units to value and returns
        // their count, -1 if malformed, -6 if absent or not a string, -3 if value is too small.
        internal static int Find(byte[] image, int start, int length, int[] keyUnits, int keyLength, int[] value)
        {
            int end = start + length;
            if (length < 12 || end > image.Length || ImageBytes.U32(image, (uint)start) != 0xBEEFCACE) return -1;
            int skip = I32(image, start + 8);
            int at = start + 12;
            if (skip < 0 || skip > end - at) return -1;
            at += skip;
            if (end - at < 12 || I32(image, at) != 2) return -1;
            int count = I32(image, at + 4), types = I32(image, at + 8);
            at += 12;
            if (count < 0 || types < 0) return -1;
            var next = new int[1];
            for (int t = 0; t < types; t++)
            {
                int size = SevenBit(image, at, end, next);
                if (size < 0 || size > end - next[0]) return -1;
                at = next[0] + size;
            }
            // Padding to an 8-byte boundary, measured from the stream start.
            while (((at - start) & 7) != 0) at++;
            if (count > (end - at) / 8 - 1) return -1;
            int positions = at + 4 * count;
            int dataSection = start + I32(image, positions + 4 * count);
            int names = positions + 4 * count + 4;
            if (dataSection < names || dataSection > end) return -1;
            for (int i = 0; i < count; i++)
            {
                int position = I32(image, positions + 4 * i);
                if (position < 0 || position > dataSection - names) return -1;
                int entry = names + position;
                int bytes = SevenBit(image, entry, dataSection, next);
                if (bytes < 0 || (bytes & 1) != 0 || bytes > dataSection - next[0] - 4) return -1;
                int name = next[0];
                if (bytes / 2 != keyLength) continue;
                bool same = true;
                for (int u = 0; u < keyLength && same; u++)
                    same = (image[name + 2 * u] | (image[name + 2 * u + 1] << 8)) == keyUnits[u];
                if (!same) continue;
                int data = dataSection + I32(image, name + bytes);
                if (data < dataSection || data >= end) return -1;
                int type = SevenBit(image, data, end, next);
                if (type != StringTypeCode) return -6;
                int utf8 = SevenBit(image, next[0], end, next);
                if (utf8 < 0 || utf8 > end - next[0]) return -1;
                return DecodeUtf8(image, next[0], utf8, value);
            }
            return -6;
        }

        private static int DecodeUtf8(byte[] image, int at, int length, int[] value)
        {
            int units = 0, end = at + length;
            while (at < end)
            {
                int b = image[at++], code;
                if (b < 0x80) code = b;
                else if ((b & 0xe0) == 0xc0 && at < end) code = ((b & 0x1f) << 6) | (image[at++] & 0x3f);
                else if ((b & 0xf0) == 0xe0 && end - at >= 2)
                {
                    code = ((b & 0x0f) << 12) | ((image[at] & 0x3f) << 6) | (image[at + 1] & 0x3f);
                    at += 2;
                }
                else if ((b & 0xf8) == 0xf0 && end - at >= 3)
                {
                    code = ((b & 0x07) << 18) | ((image[at] & 0x3f) << 12) | ((image[at + 1] & 0x3f) << 6) | (image[at + 2] & 0x3f);
                    at += 3;
                }
                else return -1;
                if (code >= 0x10000)
                {
                    if (units > value.Length - 2) return -3;
                    code -= 0x10000;
                    value[units++] = 0xd800 + (code >> 10);
                    value[units++] = 0xdc00 + (code & 0x3ff);
                }
                else
                {
                    if (units == value.Length) return -3;
                    value[units++] = code;
                }
            }
            return units;
        }

        // A 7-bit encoded Int32 (BinaryReader.Read7BitEncodedInt).
        private static int SevenBit(byte[] image, int at, int end, int[] next)
        {
            int value = 0;
            for (int shift = 0; shift < 35; shift += 7)
            {
                if (at >= end) return -1;
                int b = image[at++];
                value |= (b & 0x7f) << shift;
                if ((b & 0x80) == 0)
                {
                    next[0] = at;
                    return value;
                }
            }
            return -1;
        }

        private static int I32(byte[] image, int at) => unchecked((int)ImageBytes.U32(image, (uint)at));
    }
}
