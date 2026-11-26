using PeLibrary;

namespace PortableCompiler
{
    internal static class SignatureTypes
    {
        internal const byte Void = 0, I32 = 1, Bytes = 2, Ints = 3;

        // Destination for a method is [result, parameters...]. Local signatures
        // write only their slots. Stack values normalize CLI small integers to i32.
        internal static int Read(byte[] image, int[] heap, int index, byte[] types, int offset, int receiver, int[] methods, int[] objects)
        {
            if (index <= 0 || index >= heap[1])
                return -2;
            bool locals = receiver < 0;
            int limit = heap[0] + heap[1];
            var next = new int[1];
            int size = CompressedUnsigned(image, heap[0] + index, limit, next);
            int cursor = next[0];
            if (size < 2 || size > limit - cursor || image[cursor] != (locals ? 7 : receiver == 0 ? 0 : 0x20))
                return -2;
            int end = cursor + size;
            int count = CompressedUnsigned(image, cursor + 1, end, next);
            cursor = next[0];
            if (count < 0 || count > end - cursor)
                return -2;
            int total = locals ? count : count + 1;
            int implicitSlots = receiver > 0 ? 1 : 0;
            if (total + implicitSlots > types.Length - offset)
                return -3;
            for (int i = 0; i < total; i++)
            {
                if (cursor >= end)
                    return -2;
                int element = image[cursor++];
                int target = offset + i + (i == 0 ? 0 : implicitSlots);
                if (!locals && i == 0 && element == 1)
                    types[target] = Void;
                else if (element == 2 || element == 5 || element == 8 || element == 9)
                    types[target] = I32;
                else if (element == 0x1d && cursor < end)
                {
                    int item = image[cursor++];
                    if (item == 5) types[target] = Bytes;
                    else if (item == 8) types[target] = Ints;
                    else return -2;
                }
                else if (element == 0x12)
                {
                    int coded = CompressedUnsigned(image, cursor, end, next);
                    if (coded <= 0 || (coded & 3) != 0) return -2;
                    cursor = next[0];
                    int type = ObjectLayouts.Register(image, heap, methods, objects, coded >> 2);
                    if (type < 0) return type;
                    types[target] = (byte)type;
                }
                else
                    return -2;
            }
            if (cursor != end) return -2;
            if (receiver > 0)
                types[offset + 1] = (byte)receiver;
            return count + implicitSlots;
        }

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
