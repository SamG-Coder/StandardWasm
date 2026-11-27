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
        // A location is a traced alias outside the ordinary reference
        // conversion lattice. CLI int32& and unsigned int32& share one
        // verification type (ECMA-335 I.8.7.2). The 64-bit pair similarly shares a cell.
        internal const int ObjectLocation = 0x80, ObjectSpan = 0x81, CharacterSpan = 0x82;
        internal const int CharacterLocation = 0x83, WritableCharacterSpan = 0x84, BooleanLocation = 0x86, StringLocation = 0x87;
        internal const int ReadOnlyByteSpan = 0x88, StaticByteLocation = 0x89, CharacterSpanCallback = 0x8a, Int32Location = 0x8b;
        internal const int EventHandlerLocation = 0x8c, Int16Location = 0x8d, NativeIntLocation = 0x8e, ReadOnlyCharacterLocation = 0x8f;
        internal const int ReadOnlyInt32Span = 0x90, Int64Location = 0x91;
        // Reference locations retain their nominal element type in the verifier,
        // even when their Wasm representation is the shared object cell.
        private const int ReferenceLocationBase = 32768;
        internal static bool IsManagedLocation(int type) =>
            IsReferenceLocation(type) || IsPacked16ReadLocation(type) || type == BooleanLocation ||
            type == StaticByteLocation || type == Int32Location || type == Int64Location || type == NativeIntLocation;

        internal static bool IsPacked16Location(int type) => type == CharacterLocation || type == Int16Location;

        // A readonly span anchor uses the same Wasm carrier, with a distinct
        // verifier type so it cannot become a writable managed argument.
        internal static bool IsPacked16ReadLocation(int type) =>
            IsPacked16Location(type) || type == ReadOnlyCharacterLocation;

        internal static bool IsReferenceLocation(int type) =>
            type == ObjectLocation || type == StringLocation || type == EventHandlerLocation ||
            type >= ReferenceLocationBase + ObjectLayouts.FirstClass &&
            type < ReferenceLocationBase + ObjectLayouts.FirstClass + ObjectLayouts.MaxClasses;

        internal static int LocationFromIdentity(int[] objects, int type) =>
            type == ObjectLayouts.ObjectReference ? ObjectLocation : type == String ? StringLocation :
            type == ObjectLayouts.EventHandlerReference ? EventHandlerLocation :
            type == GenericScalar.Key(3) ? CharacterLocation : type == GenericScalar.Key(2) ? BooleanLocation :
            type == GenericScalar.Key(6) ? Int16Location :
            type == GenericScalar.Key(0x18) ? NativeIntLocation :
            type == GenericScalar.Key(5) ? StaticByteLocation :
            type == GenericScalar.Key(8) || type == GenericScalar.Key(9) ? Int32Location :
            type == GenericScalar.Key(0x0a) || type == GenericScalar.Key(0x0b) ? Int64Location :
            ObjectLayouts.IsValueType(objects, type) ? ValueLocation(type) :
            type >= ObjectLayouts.FirstClass && type - ObjectLayouts.FirstClass < objects[0] ? ReferenceLocationBase + type : 0;
        internal static int CellValue(int location) =>
            IsValueLocation(location) ? LocatedValue(location) :
            location == BooleanLocation || location == Int32Location ? I32 : location == Int64Location ? I64 :
            location == NativeIntLocation ? Native : location == StringLocation ? String :
            location == ObjectLocation ? ObjectLayouts.ObjectReference :
            location == EventHandlerLocation ? ObjectLayouts.EventHandlerReference : location - ReferenceLocationBase;

        internal static bool IsSpan(int type) => type == ObjectSpan || type == CharacterSpan || type == WritableCharacterSpan ||
            type == ReadOnlyByteSpan || type == ReadOnlyInt32Span;
        internal const byte NullPointer = PointerStorage.NullPointer, NativeZero = PointerStorage.NativeZero;
        // Keep the located CLI type in the verifier even though all cells share a Wasm heap.
        private const int ValueLocationBase = 65536;
        // This address only names argument zero in the enclosing value constructor.
        internal const int ConstructorAddress = ValueLocationBase - 1;
        internal static int ValueLocation(int type) => ValueLocationBase + type;
        internal static bool IsValueLocation(int type) => type >= ValueLocationBase;
        internal static int LocatedValue(int type) => type - ValueLocationBase;

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

        // Widen classes, strings, and primitive GC vectors without allocating or losing identity.
        // Boxing and other CLR reference conversions use separate IL operations.
        internal static bool IsObjectReference(int type) =>
            type == String || type == CharacterSpanCallback || type == ObjectLayouts.DelegateReference || RuntimeTypeMetadata.IsReference(type) || IsArray(type) ||
            DelegateShapes.IsKey(type) || // Stage 204/221: general delegates are invocation lists (decision 0092)
            type >= ObjectLayouts.FirstClass && type < ObjectLayouts.FirstClass + ObjectLayouts.MaxClasses ||
            type >= ObjectLayouts.ObjectReference && type <= ObjectLayouts.EventHandlerReference;

        // Zero denotes the CLI null literal in a live stack slot and void in a method signature.
        internal const byte NullValue = 0;

        internal static bool IsReference(int type) =>
            type == Delegate || IsObjectReference(type);

        internal static bool ReferenceComparison(int left, int right, int[] objects) =>
            (left == NullValue && (right == NullValue || IsReference(right))) ||
            (right == NullValue && IsReference(left)) ||
            (IsObjectReference(left) && IsObjectReference(right) &&
                (Assignable(left, right, objects) || Assignable(right, left, objects)));

        internal static bool Assignable(int source, int target, int[] objects)
        {
            int expected = StackType(target);
            if (source == expected) return true;
            if (source == CharacterLocation && expected == ReadOnlyCharacterLocation) return true;
            if (IsManagedLocation(source) || IsManagedLocation(expected)) return false;
            if (source >= ConstructorAddress || expected >= ConstructorAddress) return false;
            if (ObjectLayouts.IsValueType(objects, source) || ObjectLayouts.IsValueType(objects, expected)) return false;
            return source == NullValue && IsReference(expected) ||
                source == NativeZero && (target == Native || target == NullPointer) ||
                target == ObjectLayouts.ObjectReference && IsObjectReference(source) ||
                target == ObjectLayouts.DelegateReference && source == ObjectLayouts.EventHandlerReference ||
                target == ObjectLayouts.DelegateReference && DelegateShapes.IsKey(source) ||
                DelegateShapes.IsKey(source) && DelegateShapes.IsKey(expected) &&
                    DelegateVariance.Assignable(objects, source, expected) ||
                ObjectLayouts.Inherits(objects, source, expected) || ObjectLayouts.Implements(objects, source, expected);
        }

        internal static byte FloatWidth(int left, int right) => left == F64 || right == F64 ? F64 : F32;

        // Stage 164 (decision 0088): the storage of a GENERICINST delegate type, shared by method
        // and local signatures (Read) and field storage (BundleLayouts.ReadFieldStorage), so one
        // closed delegate type has one representation wherever it is declared. Func`1<int32> is
        // the scalar Delegate closure; 0 means "not a recognized delegate", and the caller reads
        // the type as an ordinary generic instance. next[0] receives the position after it.
        internal static int GenericDelegateStorage(byte[] image, int[] heaps, int[] layout, int start, int end, int[] next)
        {
            int after = DelegateMetadata.ParseFunc(image, heaps, layout, start, end);
            if (after < 0) return 0;
            next[0] = after;
            return Delegate;
        }

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

        internal static byte EnumArrayStorage(int[] objects, int array)
        {
            if (!ObjectLayouts.IsEnumArray(objects, array)) return 0;
            int element = ObjectLayouts.ArrayElement(objects, array);
            if (element == Signed8 || element == Unsigned8) return Bytes;
            if (element == Signed16 || element == Unsigned16) return Chars;
            return element == I64 ? Longs : Ints;
        }

        // Destination for a method is [result, parameters...]. Local signatures
        // write only their slots. Stack values normalize CLI small integers to i32.
        // context is the closed declaring layout that supplies VAR arguments,
        // or zero when the declaring type is not generic.
        internal static int Read(BundleImages bundle, byte[] image, int[] heap, int index, int[] types, int offset, int receiver, int[] methods, int[] objects, int assembly, int context, int[] methodArguments = null, int[] pinned = null, int[] filters = null, byte[] localElements = null)
        {
            if (index <= 0 || index >= heap[1])
                return -2;
            bool locals = receiver < 0;
            int limit = heap[0] + heap[1];
            var next = new int[1];
            int size = CompressedUnsigned(image, heap[0] + index, limit, next);
            int cursor = next[0];
            int callingConvention = locals ? 7 : receiver == 0 ? 0 : 0x20;
            if (!locals && methodArguments != null) callingConvention |= 0x10;
            if (size < 2 || size > limit - cursor || image[cursor] != callingConvention)
                return -2;
            int end = cursor + size;
            cursor++;
            if (!locals && methodArguments != null)
            {
                int arity = CompressedUnsigned(image, cursor, end, next);
                if (arity < 1 || arity != methodArguments.Length) return -2;
                cursor = next[0];
            }
            int count = CompressedUnsigned(image, cursor, end, next);
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
                int filterLocal = locals && filters != null ? ExceptionFilters.LocalKind(filters, i) : 0;
                if (filterLocal == 2 && element != 2) return -2;
                if (filterLocal == 1 && element != 0x12) return -2;
                if (locals && pinned != null) pinned[i] = 0;
                // Evaluation-stack tags can merge distinct local widths. Preserve
                // the declared element for planners that need physical storage.
                if (locals && localElements != null) localElements[i] = (byte)element;
                int target = offset + i + (i == 0 ? 0 : implicitSlots);
                if (element == PinnedLocals.Pinned)
                {
                    // Stage 149 (decision 0077): only the two fixed-string forms are pinned.
                    // A pinned string stays a String reference (Wasm GC never moves it); a
                    // pinned char& holds the native address of the frame's UTF-16 copy.
                    int kind = locals && pinned != null && cursor < end ? PinnedLocals.Kind(image, cursor, end) : 0;
                    if (kind == 0 && locals && pinned != null && end - cursor > 2 &&
                        image[cursor] == 0x10 && image[cursor + 1] == 0x11)
                    {
                        // A blittable pinned byref can hold a stable native address.
                        int storage = PointerSignatures.ReadStorage(bundle, image, methods, heap, assembly, cursor + 1, end, next);
                        if (storage != Native) return storage < 0 ? storage : -2;
                        cursor = next[0];
                        types[target] = Native;
                        continue;
                    }
                    if (kind == 0) return -2;
                    pinned[i] = kind;
                    cursor += kind == PinnedLocals.Text ? 1 : 2;
                    types[target] = kind == PinnedLocals.Text ? String : Native;
                    continue;
                }
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
                    int storage = PointerSignatures.ReadClosedStorage(bundle, image, methods, heap,
                        assembly, cursor, end, next, objects, context, methodArguments);
                    if (storage < 0) return storage;
                    cursor = next[0];
                    types[target] = storage;
                }
                else if (element == 0x1b)
                {
                    // ELEMENT_TYPE_FNPTR (decision 0078): a native-size code address, whatever
                    // its signature. Only calli reads that signature, from its own token.
                    int after = PeLibrary.SignatureExtents.Method(image, cursor, end);
                    if (after < 0) return -2;
                    cursor = after;
                    types[target] = Native;
                }
                else if (element == 0x0c || element == 0x0d)
                    types[target] = FloatStorage.FromElement(element);
                else if (element == 0x0e)
                    types[target] = String;
                else if (element == 0x1c)
                    types[target] = ObjectLayouts.ObjectReference;
                else if (element == 0x10)
                {
                    next[0] = cursor;
                    int location = ManagedReferenceSignatures.ReadLocation(bundle, image, methods, heap,
                        assembly, next, end, context, objects, methodArguments);
                    if (location < 0) return location;
                    // Internal calls return the tracked location itself. The
                    // compiler entry-point check still rejects host byref exports.
                    cursor = next[0];
                    types[target] = location;
                }
                else if (element == 0x1d && cursor < end)
                {
                    int storage = ArrayTypeRefs.Storage(image[cursor]);
                    if (storage == 0)
                    {
                        next[0] = cursor - 1;
                        storage = ManagedReferenceSignatures.ReadStorage(bundle, image, methods, heap,
                            assembly, next, end, context, objects, methodArguments);
                        if (storage < 0) return storage;
                        cursor = next[0];
                    }
                    else cursor++;
                    types[target] = storage;
                }
                else if (element == 0x15)
                {
                    var afterDelegate = new int[1];
                    int delegateStorage = GenericDelegateStorage(image, heap, methods, cursor - 1, end, afterDelegate);
                    if (delegateStorage != 0)
                    {
                        cursor = afterDelegate[0];
                        types[target] = delegateStorage;
                    }
                    else
                    {
                        next[0] = cursor - 1;
                        int type = ManagedReferenceSignatures.ReadStorage(bundle, image, methods, heap,
                            assembly, next, end, context, objects, methodArguments);
                        if (type < 0) return type;
                        cursor = next[0];
                        types[target] = type;
                    }
                }
                else if (element == 0x13 || element == 0x1e)
                {
                    // VAR and MVAR select distinct argument vectors. Convert
                    // the CLI type identity to its Wasm slot only after lookup.
                    next[0] = cursor - 1;
                    int storage = ManagedReferenceSignatures.ReadStorage(bundle, image, methods, heap,
                        assembly, next, end, context, objects, methodArguments);
                    if (storage < 0) return storage;
                    cursor = next[0];
                    types[target] = storage;
                }
                else if (element == 0x12 || element == 0x11)
                {
                    int coded = CompressedUnsigned(image, cursor, end, next);
                    if (coded <= 0) return -2;
                    cursor = next[0];
                    bool valueType = element == 0x11;
                    if (filterLocal == 1)
                    {
                        if ((coded & 3) != 1 || !ExceptionFilters.IsExceptionGuard(image, methods, heap, 0x01000000 | (coded >> 2)))
                            return -2;
                        types[target] = ObjectLayouts.ObjectReference; // Elided filter scratch.
                        continue;
                    }
                    int type = RuntimeTypeMetadata.Storage(image, heap, methods, coded, valueType);
                    // Keep the upstream System.Type layout when this bundle supplies it.
                    // The opaque descriptor is the fallback for libraries without that definition.
                    // A single-image compile has no framework assembly lookup.
                    if (type == RuntimeTypeMetadata.Type && bundle.Length != 0 &&
                        AssemblyBundle.ClassIdentity(bundle, image, methods, heap, assembly, coded, new int[2]) != -4)
                        type = 0;
                    if (type == 0)
                        type = BundleLayouts.RegisterType(bundle, image, methods, heap, assembly, coded, objects, valueType);
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
