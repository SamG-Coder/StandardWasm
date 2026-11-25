using PeLibrary;
using static PeLibrary.ImageBytes;

namespace PortableCompiler
{
    internal static class SignatureTypes
    {
        internal const byte Void = 0, I32 = 1, Bytes = 2, Ints = 3, Delegate = 4, MethodPointer = 5, String = 6, Chars = 7, I64 = WideStorage.Scalar, Longs = WideStorage.Array, F32 = 10, F64 = 11;

        internal static bool IsInteger(byte type) => type == I32 || type == I64;

        internal static bool IsFloat(byte type) => type == F32 || type == F64;

        internal static bool IsScalar(byte type) => IsInteger(type) || IsFloat(type);

        internal static byte FloatWidth(byte left, byte right) => left == F64 || right == F64 ? F64 : F32;

        internal static byte ArrayValueType(byte array) => array == Longs ? I64 : I32;

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
                else if (element == 2 || element == 3 || element == 5 || element == 8 || element == 9)
                    types[target] = I32;
                else if (WideStorage.IsIntegerElement(element))
                    types[target] = I64;
                else if (element == 0x0c || element == 0x0d)
                    types[target] = element == 0x0c ? F32 : F64;
                else if (element == 0x0e)
                    types[target] = String;
                else if (element == 0x1d && cursor < end)
                {
                    int item = image[cursor++];
                    if (item == 5) types[target] = Bytes;
                    else if (item == 3) types[target] = Chars;
                    else if (item == 8) types[target] = Ints;
                    else if (WideStorage.IsIntegerElement(item)) types[target] = Longs;
                    else return -2;
                }
                else if (element == 0x15)
                {
                    cursor = DelegateMetadata.ParseFunc(image, heap, methods, cursor - 1, end);
                    if (cursor < 0) return -2;
                    types[target] = Delegate;
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

    }
}
