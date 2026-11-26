using PeLibrary;
using static PeLibrary.ImageBytes;

namespace PortableCompiler
{
    internal static class SignatureTypes
    {
        internal const byte Void = 0, I32 = 1, Bytes = ArrayTypeRefs.Bytes, Ints = ArrayTypeRefs.Ints, Delegate = 4,
            MethodPointer = 5, String = 6, Chars = ArrayTypeRefs.Packed16, I64 = WideStorage.Scalar, Longs = WideStorage.Array,
            F32 = FloatStorage.Single, F64 = FloatStorage.Double, Singles = ArrayTypeRefs.Singles, Doubles = ArrayTypeRefs.Doubles, Native = NativeStorage.Scalar,
            NativeVector = NativeStorage.Vector;
        internal const byte NullPointer = PointerStorage.NullPointer, NativeZero = PointerStorage.NativeZero;

        // Signature and frame tags keep the CLI storage width. Narrow types
        // are distinct from evaluation-stack types.
        internal const int Signed8 = NarrowStorage.Signed8, Unsigned8 = NarrowStorage.Unsigned8,
            Signed16 = NarrowStorage.Signed16, Unsigned16 = NarrowStorage.Unsigned16, Character = NarrowStorage.Character;

        internal static bool IsNarrow(int type) => type >= Signed8 && type <= Character;
        internal static int StackType(int type) => IsNarrow(type) ? I32 : type;
        internal static bool IsInteger(int type) => type == I32 || type == I64 || IsNarrow(type);

        internal static bool IsFloat(int type) => type == F32 || type == F64;

        internal static bool IsNativeInteger(int type) => type == Native || type == NativeZero;

        internal static bool IsScalar(int type) => IsInteger(type) || IsFloat(type) || IsNativeInteger(type);

        internal static byte IntegerConversionType(int opcode) =>
            opcode == 0x6a || opcode == 0x6e ? I64 : opcode == 0xd3 || opcode == 0xe0 ? Native : I32;

        // Widen supported class references without allocating or losing sender identity.
        // Boxing and other CLR reference conversions use separate IL operations.
        internal static bool IsObjectReference(int type) =>
            type >= ObjectLayouts.FirstClass ||
            type >= ObjectLayouts.ObjectReference && type <= ObjectLayouts.EventHandlerReference;

        // Zero denotes the CLI null literal in a live stack slot and void in a method signature.
        internal const byte NullValue = 0;

        internal static bool IsReference(int type) =>
            type == String || type == Delegate || IsArray(type) || IsObjectReference(type);

        internal static bool ReferenceComparison(int left, int right, int[] objects) =>
            (left == NullValue && (right == NullValue || IsReference(right))) ||
            (right == NullValue && IsReference(left)) ||
            (IsObjectReference(left) && IsObjectReference(right) &&
                (Assignable(left, right, objects) || Assignable(right, left, objects)));

        internal static bool Assignable(int source, int target, int[] objects)
        {
            int expected = StackType(target);
            if (source == expected) return true;
            if (ObjectLayouts.IsValueType(objects, source) || ObjectLayouts.IsValueType(objects, expected)) return false;
            return source == NullValue && IsReference(expected) ||
                source == NativeZero && (target == Native || target == NullPointer) ||
                target == ObjectLayouts.ObjectReference && IsObjectReference(source) ||
                ObjectLayouts.Inherits(objects, source, expected) || ObjectLayouts.Implements(objects, source, expected);
        }

        internal static byte FloatWidth(int left, int right) => left == F64 || right == F64 ? F64 : F32;

        internal static bool IsArray(int type) =>
            type == Bytes || type == Ints || type == Chars || type == Longs || type == Singles || type == Doubles || type == NativeVector;

        internal static byte ArrayValueType(int array) =>
            array == Longs ? I64 : array == Singles ? F32 : array == Doubles ? F64 : array == NativeVector ? Native : I32;

        internal static byte ArrayLoadType(int opcode)
        {
            if (opcode <= 0x91) return Bytes;
            if (opcode <= 0x93) return Chars;
            if (opcode <= 0x95) return Ints;
            if (opcode == 0x96) return Longs;
            if (opcode == 0x97) return NativeVector;
            return opcode == 0x98 ? Singles : Doubles;
        }

        internal static byte ArrayStoreType(int opcode)
        {
            if (opcode == 0x9b) return NativeVector;
            if (opcode == 0x9c) return Bytes;
            if (opcode == 0x9d) return Chars;
            if (opcode == 0x9e) return Ints;
            if (opcode == 0x9f) return Longs;
            return opcode == 0xa0 ? Singles : Doubles;
        }

        // Destination for a method is [result, parameters...]. Local signatures
        // write only their slots. Stack values normalize CLI small integers to i32.
        // context is the closed declaring layout that supplies VAR arguments,
        // or zero when the declaring type is not generic.
        internal static int Read(byte[] main, byte[] bundle, int[] ranges, byte[] image, int[] heap, int index, int[] types, int offset, int receiver, int[] methods, int[] objects, int assembly, int context)
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
                else if (element >= 3 && element <= 7)
                    types[target] = NarrowStorage.FromElement(element);
                else if (WideStorage.IsIntegerElement(element))
                    types[target] = I64;
                else if (NativeStorage.IsIntegerElement(element))
                    types[target] = Native;
                else if (element == PointerStorage.Element)
                {
                    int storage = PointerSignatures.ReadStorage(main, bundle, ranges, image, methods, heap,
                        assembly, cursor, end, next);
                    if (storage < 0) return storage;
                    cursor = next[0];
                    types[target] = storage;
                }
                else if (element == 0x0c || element == 0x0d)
                    types[target] = FloatStorage.FromElement(element);
                else if (element == 0x0e)
                    types[target] = String;
                else if (element == 0x1c)
                    types[target] = ObjectLayouts.ObjectReference;
                else if (element == 0x1d && cursor < end)
                {
                    int storage = ArrayTypeRefs.Storage(image[cursor]);
                    if (storage == 0)
                    {
                        next[0] = cursor - 1;
                        storage = ManagedReferenceSignatures.ReadStorage(main, bundle, ranges, image, methods, heap,
                            assembly, next, end, context, objects);
                        if (storage < 0) return storage;
                        cursor = next[0];
                    }
                    else cursor++;
                    types[target] = storage;
                }
                else if (element == 0x15)
                {
                    int afterDelegate = DelegateMetadata.ParseFunc(image, heap, methods, cursor - 1, end);
                    if (afterDelegate >= 0)
                    {
                        cursor = afterDelegate;
                        types[target] = Delegate;
                    }
                    else
                    {
                        next[0] = cursor - 1;
                        int type = ManagedReferenceSignatures.ReadStorage(main, bundle, ranges, image, methods, heap,
                            assembly, next, end, context, objects);
                        if (type < 0) return type;
                        cursor = next[0];
                        types[target] = type;
                    }
                }
                else if (element == 0x13)
                {
                    // A class type parameter takes the storage of the closed
                    // argument. int and uint share i32 storage here; their
                    // signed and unsigned semantics stay in the caller's IL.
                    int number = CompressedUnsigned(image, cursor, end, next);
                    if (number < 0) return -2;
                    cursor = next[0];
                    int argument = ObjectLayouts.Argument(objects, context, number);
                    if (argument < 0) return argument;
                    types[target] = ObjectLayouts.SignatureStorage(objects, argument);
                }
                else if (element == 0x12 || element == 0x11)
                {
                    int coded = CompressedUnsigned(image, cursor, end, next);
                    if (coded <= 0) return -2;
                    cursor = next[0];
                    int type = BundleLayouts.RegisterType(main, bundle, ranges, image, methods,
                        heap, assembly, coded, objects, element == 0x11);
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
