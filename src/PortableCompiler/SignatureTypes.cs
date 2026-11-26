using PeLibrary;
using static PeLibrary.ImageBytes;

namespace PortableCompiler
{
    internal static class SignatureTypes
    {
        internal const byte Void = 0, I32 = 1, Bytes = ArrayTypeRefs.Bytes, Ints = ArrayTypeRefs.Ints, Delegate = 4,
            MethodPointer = 5, String = 6, Chars = ArrayTypeRefs.Packed16, I64 = WideStorage.Scalar, Longs = WideStorage.Array,
            F32 = 10, F64 = 11, Singles = ArrayTypeRefs.Singles, Doubles = ArrayTypeRefs.Doubles;

        // Signature and frame tags keep the CLI storage width. Narrow types
        // live above the byte range reserved for evaluation stack types.
        internal const int Signed8 = 256, Unsigned8 = 257, Signed16 = 258, Unsigned16 = 259, Character = 260;

        internal static bool IsNarrow(int type) => type >= Signed8 && type <= Character;
        internal static byte StackType(int type) => IsNarrow(type) ? I32 : unchecked((byte)type);
        internal static bool IsInteger(int type) => type == I32 || type == I64 || IsNarrow(type);

        internal static bool IsFloat(int type) => type == F32 || type == F64;

        internal static bool IsScalar(int type) => IsInteger(type) || IsFloat(type);

        // Widen supported class references without allocating or losing sender identity.
        // Boxing and other CLR reference conversions use separate IL operations.
        internal static bool IsObjectReference(byte type) =>
            type >= ObjectLayouts.FirstClass ||
            type >= ObjectLayouts.ObjectReference && type <= ObjectLayouts.EventHandlerReference;

        internal static bool Assignable(byte source, int target) =>
            source == StackType(target) || target == ObjectLayouts.ObjectReference && IsObjectReference(source);

        internal static byte FloatWidth(byte left, byte right) => left == F64 || right == F64 ? F64 : F32;

        internal static bool IsArray(byte type) =>
            type == Bytes || type == Ints || type == Chars || type == Longs || type == Singles || type == Doubles;

        internal static byte ArrayValueType(byte array) =>
            array == Longs ? I64 : array == Singles ? F32 : array == Doubles ? F64 : I32;

        internal static byte ArrayLoadType(int opcode)
        {
            if (opcode <= 0x91) return Bytes;
            if (opcode <= 0x93) return Chars;
            if (opcode <= 0x95) return Ints;
            if (opcode == 0x96) return Longs;
            return opcode == 0x98 ? Singles : Doubles;
        }

        internal static byte ArrayStoreType(int opcode)
        {
            if (opcode == 0x9c) return Bytes;
            if (opcode == 0x9d) return Chars;
            if (opcode == 0x9e) return Ints;
            if (opcode == 0x9f) return Longs;
            return opcode == 0xa0 ? Singles : Doubles;
        }

        // Destination for a method is [result, parameters...]. Local signatures
        // write only their slots. Stack values normalize CLI small integers to i32.
        internal static int Read(byte[] image, int[] heap, int index, int[] types, int offset, int receiver, int[] methods, int[] objects, int assembly)
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
                else if (element == 2 || element == 8 || element == 9)
                    types[target] = I32;
                else if (element == 3)
                    types[target] = Character;
                else if (element >= 4 && element <= 7)
                    types[target] = Signed8 + element - 4;
                else if (WideStorage.IsIntegerElement(element))
                    types[target] = I64;
                else if (element == 0x0c || element == 0x0d)
                    types[target] = element == 0x0c ? F32 : F64;
                else if (element == 0x0e)
                    types[target] = String;
                else if (element == 0x1c)
                    types[target] = ObjectLayouts.ObjectReference;
                else if (element == 0x1d && cursor < end)
                {
                    byte storage = ArrayTypeRefs.Storage(image[cursor++]);
                    if (storage == 0) return -2;
                    types[target] = storage;
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
                    if (coded <= 0) return -2;
                    cursor = next[0];
                    int type;
                    if ((coded & 3) == 0)
                        type = ObjectLayouts.Register(image, heap, methods, objects, assembly, coded >> 2);
                    else
                    {
                        type = EventMetadata.Reference(image, heap, methods, coded);
                        if (type == 0) return -2;
                    }
                    if (type < 0) return type;
                    types[target] = type;
                }
                else
                    return -2;
            }
            if (cursor != end) return -2;
            if (receiver > 0)
                types[offset + 1] = receiver;
            return count + implicitSlots;
        }

    }
}
