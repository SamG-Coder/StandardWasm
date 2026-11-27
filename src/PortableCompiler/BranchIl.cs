using PeLibrary;

namespace PortableCompiler
{
    internal static class BranchIl
    {
        internal const int Width = 4;
        internal const int Load = 1, Store = 2, Constant = 3, Unary = 4, Binary = 5,
            Return = 6, Branch = 7, Conditional = 8, Nop = 9, Dup = 10, Pop = 11, Call = 12, NewArray = 13, ArrayLength = 14, ArrayGet = 15, ArraySet = 16, Narrow = 17, NewObject = 18, GetField = 19, SetField = 20, BaseCtor = 21, LoadFunction = 22, NewDelegate = 23, InvokeDelegate = 24, MemberCall = 25, LoadString = 26, StringLength = 27, StringCharacter = 28, JoinStrings = 29, StringFromChars = 30, NullLiteral = 31, WideConstant = 32, ConvertInteger = 33, Switch = 34, SingleConstant = 35, DoubleConstant = 36, ConvertFloat = 37;
        internal const int UnsignedSingle = 0x100;
        internal const int LoadEventArgsEmpty = 38, NewEventArgs = 39, CombineHandler = 40, CastHandler = 41, RemoveHandler = 42;
        internal const int FieldAddress = 43, MethodSpecCall = 44, ExchangeHandlerField = 45;
        internal const int FieldToken = 46, InitializedArray = 47;
        internal const int CheckedInteger = 48, CheckedConversion = 49, Leave = 50, Rethrow = 51, EndFinally = 52;
        internal const int CastClass = 53, TestClass = 54, ThrowNull = 55;
        internal const int FrameAddress = 56, InitializeValue = 57, UpdateValue = 58, ClearValue = 59;
        // Console.Out/Error push a host stream; the operand is the stream number.
        // ConsoleWrite consumes a stream and, unless writing an empty line, a String.
        internal const int ConsoleWriter = 61, ConsoleWrite = 62;
        internal const int LoadStaticField = 63, StoreStaticField = 64;
        internal const int VirtualCall = 65;
        // call S::.ctor on the managed address of a local or of a value
        // constructor's receiver: [S, arguments] -> []. The operand is the
        // function and the auxiliary field is the addressed frame index.
        internal const int ConstructValue = 66;
        internal const int LoadTypeToken = 67, TypeFromHandle = 68;
        // newobj System.Object::.ctor: a fresh plain object (decision 0057). [] -> [Object].
        internal const int NewRootObject = 69;
        // ldind.* / stind.* through an unmanaged pointer into memory 0 (decision 0060).
        // [address] -> [value] and [address, value] -> []. The operand is the opcode.
        internal const int LoadIndirect = 70, StoreIndirect = 71;
        // localloc: [size] -> [address] on the shadow stack in memory 0.
        internal const int LocalAllocate = 72;
        internal const int ReadReflection = 73, StringEquals = 74, ResourceLookup = 75;
        internal const int TakeValueFieldAddress = 76;
        internal const int LoadConstructorAddress = 78, UpdateConstructorField = 79;
        internal const int BoxValue = 80, UnboxValue = 81, TestBox = 82, ConstrainedStatic = 83;
        // A traced address is different from a native linear-memory address.
        internal const int ObjectElementAddress = 100, LoadObjectIndirect = 101, StoreObjectIndirect = 102;
        internal const int LoadInlineStorage = 103, BoundObjectElement = 1;
        internal const int NewObjectSpan = 104, SpanLength = 105, SpanRead = 106, StringAsSpan = 108;
        internal const int CharacterArrayAsSpan = 109, SpanAddress = 110, SpanAsReadOnly = 111;
        // sizeof <type>: lowered to an int32 Constant once the layout is measured.
        internal const int GetObjectType = 116, EqualRuntimeTypes = 117, Win32Error = 119, NativePointerConstructor = 120;
        internal const int SizeOf = 112; // clear of the reflection kinds 73–75 (Stage 131)
        // ldobj / stobj / initobj of a blittable struct through a native address (decision 0073).
        // Operand: the registry value type; auxiliary: native plan index + 1.
        internal const int LoadObject = 113, StoreObject = 114, InitializeNative = 115; // clear of Stage 131
        // A fixed string's address (decision 0077): [String] -> [Native], a frame-owned UTF-16
        // copy with a NUL terminator, or 0 for null. Auxiliary 1: null raises NullReferenceException.
        // 118 matches the integration line, where 116 and 117 are runtime-type kinds.
        internal const int PinString = 118;
        internal const int StaticFieldAddress = 121, LoadNativeStatic = 122, StoreNativeStatic = 123, Monitor = 124, FilterSkip = 125, LoadSingleObjectSpan = 126;
        internal const int LoadFrameLocation = 127;
        internal const int LoadManagedCell = 128, StoreManagedCell = 129, ClearManagedCell = 130;
        internal const int WidenReference = 131, ConstrainedInstance = 132;
        internal const int StringFirstCodeUnit = 133, LoadStaticByteSpan = 134;
        internal const int TakeStaticByteLocation = 135, OffsetStaticByteLocation = 136, ReadStaticUInt16 = 137;
        // Managed function pointers (decision 0078). CallIndirect: calli, operand the
        // StandAloneSig row, then linked to a carrier function index. FunctionAddress:
        // ldftn into a pointer, operand the table-2 slot. [] -> [Native].
        internal const int CallIndirect = 138, FunctionAddress = 139;
        internal const int StringRange = 140;
        internal const int SuppressFinalizer = 141; // [object] -> []
        internal const int NativeCharacterSpan = 142, BoundScopedFieldLoad = 143, BoundScopedFieldStore = 144;
        // [managed address of the containing struct] -> []; replace one field with its CLI default.
        internal const int ClearInteriorField = 145;
        // Console.ReadLine: [] -> [String or null at EOF].
        internal const int ConsoleReadLine = 146;
        internal const int StaticInt32 = 147;
        internal const int AllocateString = 148, StringConstructionSpan = 149, ThrowFrameworkArgument = 150;
        internal const int ProvenNativeZero = 2;
        internal const int MethodSpecCallvirt = 2;
        // A platform System.Math call lowered in place. The operand is a PlatformMembers intrinsic id.
        internal const int MathCall = 60;

        // Records are keyed by IL byte offset. Operand bytes retain kind zero.
        // Record fields are kind, operand, next IL offset and auxiliary operand (such as a comparison or high literal word).
        internal static int Decode(byte[] image, int start, int length, int arguments, int locals,
            int[] records, int[] location, bool hasCatches, int first = 0, int[] filterEnds = null)
        {
            int pc = first;
            while (pc < length)
            {
                if (location.Length != 0) location[2] = pc;
                int slot = pc * Width;
                if (filterEnds != null && filterEnds[pc] != 0)
                {
                    pc = filterEnds[pc];
                    records[slot] = FilterSkip;
                    records[slot + 2] = pc;
                    continue;
                }
                int op = image[start + pc++];
                if (op == 0xfe && pc < length && image[start + pc] == 0x13)
                {
                    pc++;
                    if (pc >= length) return -1;
                    op = image[start + pc++];
                    // This module owns its GC fields on one Wasm agent.
                    // Keep each field operation in sequence. Shared or
                    // address-based accesses need their own memory model.
                    if (op != 0x7b && op != 0x7d) return -2;
                }
                int kind, operand = 0, comparison = 0;
                if (op >= 2 && op <= 5 || op == 0x0e)
                {
                    kind = Load;
                    operand = op - 2;
                    if (op == 0x0e)
                    {
                        if (pc >= length) return -1;
                        operand = image[start + pc++];
                    }
                    if (operand >= arguments) return -1;
                }
                else if (op == 0x0f || op == 0x10)
                {
                    kind = op == 0x0f ? FrameAddress : Store;
                    if (pc >= length) return -1;
                    operand = image[start + pc++];
                    if (operand >= arguments) return -1;
                }
                else if (op >= 6 && op <= 13 || op >= 0x11 && op <= 0x13)
                {
                    bool store = op == 0x13 || (op >= 10 && op <= 13);
                    kind = op == 0x12 ? FrameAddress : store ? Store : Load;
                    if (op >= 0x11)
                    {
                        if (pc >= length) return -1;
                        operand = image[start + pc++];
                    }
                    else
                    {
                        operand = op - (store ? 10 : 6);
                    }
                    if (operand >= locals) return -1;
                    operand += arguments;
                }
                else if (op >= 0x15 && op <= 0x20)
                {
                    kind = Constant;
                    operand = op - 0x16;
                    if (op == 0x1f)
                    {
                        if (pc >= length) return -1;
                        operand = SignedByte(image[start + pc++]);
                    }
                    else if (op == 0x20)
                    {
                        if (length - pc < 4) return -1;
                        operand = unchecked((int)ImageBytes.U32(image, (uint)(start + pc)));
                        pc += 4;
                    }
                }
                else if (op == 0x21)
                {
                    if (length - pc < 8) return -1;
                    kind = WideConstant;
                    operand = unchecked((int)ImageBytes.U32(image, (uint)(start + pc)));
                    comparison = unchecked((int)ImageBytes.U32(image, (uint)(start + pc + 4)));
                    pc += 8;
                }
                else if (op == 0x22 || op == 0x23)
                {
                    int size = op == 0x22 ? 4 : 8;
                    if (length - pc < size) return -1;
                    kind = op == 0x22 ? SingleConstant : DoubleConstant;
                    operand = unchecked((int)ImageBytes.U32(image, (uint)(start + pc)));
                    if (size == 8) comparison = unchecked((int)ImageBytes.U32(image, (uint)(start + pc + 4)));
                    pc += size;
                }
                else if (op >= 0x2b && op <= 0x44)
                {
                    int branch = op <= 0x37 ? op - 0x2b : op - 0x38;
                    if (op >= 0x38)
                    {
                        if (length - pc < 4) return -1;
                        operand = unchecked((int)ImageBytes.U32(image, (uint)(start + pc)));
                        pc += 4;
                    }
                    else
                    {
                        if (pc >= length) return -1;
                        operand = SignedByte(image[start + pc++]);
                    }
                    if (operand < -pc || operand >= length - pc) return -1;
                    operand += pc;
                    kind = branch == 0 ? Branch : Conditional;
                    comparison = Comparison(branch);
                }
                else if (op == 0x45)
                {
                    if (length - pc < 4) return -1;
                    uint count = ImageBytes.U32(image, (uint)(start + pc));
                    pc += 4;
                    if (count > (uint)((length - pc) / 4)) return -1;
                    kind = Switch;
                    comparison = (int)count;
                    operand = pc;
                    pc += comparison * 4;
                    if (pc >= length) return -1;

                    // Offsets are relative to the end of the table. Keep target
                    // offsets in the unused operand records, with kind still zero.
                    for (int i = 0; i < comparison; i++)
                    {
                        int entry = operand + i * 4;
                        int delta = unchecked((int)ImageBytes.U32(image, (uint)(start + entry)));
                        if (delta < -pc || delta >= length - pc) return -1;
                        records[entry * Width + 1] = pc + delta;
                    }
                }
                else if (op == 0xdc)
                {
                    if (!hasCatches) return -1;
                    kind = EndFinally;
                }
                else if (op == 0xdd || op == 0xde)
                {
                    int size = op == 0xdd ? 4 : 1;
                    if (length - pc < size) return -1;
                    int delta = size == 4 ? unchecked((int)ImageBytes.U32(image, (uint)(start + pc))) : SignedByte(image[start + pc]);
                    pc += size;
                    if (delta < -pc || delta >= length - pc) return -1;
                    operand = pc + delta;
                    kind = Leave;
                }
                else if (op == 0x28 || op == 0x6f || op == 0x73 || op == 0x7b || op == 0x7c || op == 0x7d || op == 0x7e || op == 0x7f || op == 0x80)
                {
                    if (length - pc < 4) return -1;
                    uint token = ImageBytes.U32(image, (uint)(start + pc));
                    pc += 4;
                    uint table = token >> 24;
                    operand = (int)(token & 0xffffff);
                    if (operand == 0) return -1;
                    if (op == 0x7b || op == 0x7c || op == 0x7d)
                    {
                        if (table != 4 && table != 0x0a) return -2;
                        kind = op == 0x7b ? GetField : op == 0x7c ? FieldAddress : SetField;
                        comparison = table == 0x0a ? 1 : 0;
                    }
                    else if (op == 0x7e || op == 0x7f || op == 0x80)
                    {
                        if (table != 4 && table != 0x0a) return -2;
                        kind = op == 0x7e ? LoadStaticField : op == 0x7f ? StaticFieldAddress : StoreStaticField;
                        comparison = table == 0x0a ? 1 : 0;
                    }
                    else if (table == 0x2b && (op == 0x28 || op == 0x6f))
                    {
                        kind = MethodSpecCall;
                        comparison = op == 0x6f ? MethodSpecCallvirt : 0;
                    }
                    else if (table == 0x0a)
                    {
                        kind = MemberCall;
                        comparison = op;
                    }
                    else
                    {
                        if (table != 6) return -2;
                        kind = op == 0x73 ? NewObject : Call;
                        comparison = op == 0x6f ? 1 : 0;
                    }
                }
                else if (op == 0x29)
                {
                    if (length - pc < 4) return -1;
                    uint token = ImageBytes.U32(image, (uint)(start + pc));
                    pc += 4;
                    if (token >> 24 != 0x11 || (token & 0xffffff) == 0) return -1;
                    kind = CallIndirect;
                    operand = (int)(token & 0xffffff);
                }
                else if (op == 0x74 || op == 0x75)
                {
                    if (length - pc < 4) return -1;
                    uint token = ImageBytes.U32(image, (uint)(start + pc));
                    pc += 4;
                    uint table = token >> 24;
                    if (table != 1 && table != 2) return -2;
                    int row = (int)(token & 0xffffff);
                    if (row == 0) return -1;
                    kind = op == 0x74 ? CastClass : TestClass;
                    operand = (row << 2) | (table == 1 ? 1 : 0);
                }
                else if (op == 0x14) kind = NullLiteral;
                else if (op == 0x7a) kind = ThrowNull; // Stack analysis verifies the operand.
                else if (op == 0x72)
                {
                    if (length - pc < 4) return -1;
                    uint token = ImageBytes.U32(image, (uint)(start + pc));
                    pc += 4;
                    if ((token >> 24) != 0x70) return -1;
                    kind = LoadString;
                    operand = (int)(token & 0xffffff);
                }
                else if (op == 0x8c || op == 0xa5)
                {
                    if (length - pc < 4) return -1;
                    operand = unchecked((int)ImageBytes.U32(image, (uint)(start + pc)));
                    pc += 4;
                    kind = op == 0x8c ? BoxValue : UnboxValue;
                }
                else if (op == 0x8d)
                {
                    kind = NewArray;
                    if (length - pc < 4) return -1;
                    operand = unchecked((int)ImageBytes.U32(image, (uint)(start + pc)));
                    pc += 4;
                }
                else if (op == 0xd0)
                {
                    if (length - pc < 4) return -1;
                    uint token = ImageBytes.U32(image, (uint)(start + pc));
                    pc += 4;
                    operand = (int)(token & 0xffffff);
                    uint table = token >> 24;
                    if (table == 4) kind = FieldToken;
                    else if (table == 1 || table == 2 || table == 0x1b)
                    {
                        if (operand == 0) return -1;
                        kind = LoadTypeToken;
                        operand = (operand << 2) | (table == 2 ? 0 : table == 1 ? 1 : 2);
                    }
                    else return -2;
                }
                else if (op == 0x8e) kind = ArrayLength;
                else if (op == 0x8f)
                {
                    if (length - pc < 4) return -1;
                    uint token = ImageBytes.U32(image, (uint)(start + pc));
                    pc += 4;
                    uint table = token >> 24;
                    if (table != 1 && table != 2 && table != 0x1b) return -2;
                    int row = (int)(token & 0xffffff);
                    if (row == 0) return -1;
                    operand = row << 2 | (table == 1 ? 1 : table == 2 ? 0 : 2);
                    kind = ObjectElementAddress;
                }
                else if (op >= 0x90 && op <= 0x9a)
                {
                    kind = ArrayGet;
                    operand = op;
                }
                else if (op >= 0x9b && op <= 0xa2)
                {
                    kind = ArraySet;
                    operand = op;
                }
                else if (op == 0xa3 || op == 0xa4)
                {
                    if (length - pc < 4) return -1;
                    comparison = unchecked((int)ImageBytes.U32(image, (uint)(start + pc)));
                    pc += 4;
                    if (comparison == 0) return -1;
                    kind = op == 0xa3 ? ArrayGet : ArraySet;
                }
                else if (op == 0xd2 || op == 0xd1 || op == 0x67 || op == 0x68)
                {
                    kind = Narrow;
                    operand = op;
                }
                else if (op == 0xfe)
                {
                    if (pc >= length) return -1;
                    int extended = image[start + pc++];
                    if (extended == 6)
                    {
                        if (length - pc < 4) return -1;
                        uint token = ImageBytes.U32(image, (uint)(start + pc));
                        pc += 4;
                        if (token >> 24 != 6) return -2;
                        kind = LoadFunction;
                        operand = (int)(token & 0xffffff);
                    }
                    else if (extended == 0x0a || extended == 0x0d)
                    {
                        if (length - pc < 2) return -1;
                        operand = (int)ImageBytes.U16(image, (uint)(start + pc));
                        pc += 2;
                        bool argument = extended == 0x0a;
                        if (operand >= (argument ? arguments : locals)) return -1;
                        kind = FrameAddress;
                        if (!argument) operand += arguments;
                    }
                    else if (extended == 0x15)
                    {
                        if (length - pc < 4) return -1;
                        operand = unchecked((int)ImageBytes.U32(image, (uint)(start + pc)));
                        pc += 4;
                        kind = InitializeValue;
                    }
                    else if (extended == 0x0f)
                        kind = LocalAllocate;
                    else if (extended == 0x16)
                    {
                        if (length - pc < 9) return -1;
                        operand = unchecked((int)ImageBytes.U32(image, (uint)(start + pc)));
                        pc += 4;
                        int call = image[start + pc++];
                        if (call != 0x28 && call != 0x6f) return -2;
                        uint member = ImageBytes.U32(image, (uint)(start + pc));
                        pc += 4;
                        uint table = member >> 24;
                        if ((member & 0xffffff) == 0 || (table != 0x0a && (call != 0x6f || table != 6))) return -2;
                        kind = call == 0x28 ? ConstrainedStatic : ConstrainedInstance;
                        comparison = kind == ConstrainedStatic ? (int)(member & 0xffffff) : (int)member;
                    }
                    else if (extended == 0x1c)
                    {
                        if (length - pc < 4) return -1;
                        operand = unchecked((int)ImageBytes.U32(image, (uint)(start + pc)));
                        pc += 4;
                        kind = SizeOf;
                    }
                    else if (extended == 0x1a)
                    {
                        if (!hasCatches) return -1;
                        kind = Rethrow;
                    }
                    else
                    {
                        if (extended < 1 || extended > 5) return -2;
                        kind = Binary;
                        operand = extended == 1 ? 0x46 : Comparison(extended == 2 ? 5 :
                            extended == 3 ? 10 : extended == 4 ? 7 : 12);
                    }
                }
                else if (op >= 0xd6 && op <= 0xdb)
                {
                    kind = CheckedInteger;
                    operand = op;
                }
                else if (op >= 0x82 && op <= 0x8b || op >= 0xb3 && op <= 0xba || op == 0xd4 || op == 0xd5)
                {
                    kind = CheckedConversion;
                    operand = op;
                }
                else if (op == 0x65 || op == 0x66)
                {
                    kind = Unary;
                    operand = op == 0x65 ? 0x6c : 0x73;
                }
                else if (op == 0x69 || op == 0x6a || op == 0x6d || op == 0x6e || op == 0xd3 || op == 0xe0)
                {
                    kind = ConvertInteger;
                    operand = op;
                }
                else if (op == 0x6b || op == 0x6c || op == 0x76)
                {
                    kind = ConvertFloat;
                    operand = op;
                }
                else if (op == 0x50 || op == 0x51)
                    kind = op == 0x50 ? LoadObjectIndirect : StoreObjectIndirect;
                else if (op >= 0x46 && op <= 0x4f || op >= 0x52 && op <= 0x57 || op == 0xdf)
                {
                    kind = op <= 0x4f ? LoadIndirect : StoreIndirect;
                    operand = op;
                }
                else if (op == 0x71 || op == 0x81)
                {
                    if (length - pc < 4) return -1;
                    operand = unchecked((int)ImageBytes.U32(image, (uint)(start + pc)));
                    pc += 4;
                    kind = op == 0x71 ? LoadObject : StoreObject;
                }
                else if (op == 0) kind = Nop;
                else if (op == 0x2a) kind = Return;
                else if (op == 0x25) kind = Dup;
                else if (op == 0x26) kind = Pop;
                else
                {
                    kind = Binary;
                    operand = op == 0x5d ? 0x6f : StraightLineIl.Binary(op);
                    if (operand < 0) return -2;
                }
                records[slot] = kind;
                records[slot + 1] = operand;
                records[slot + 2] = pc;
                records[slot + 3] = comparison;
            }

            for (int pcIndex = 0; pcIndex < length; pcIndex += records[pcIndex * Width + 2] - pcIndex)
            {
                if (location.Length != 0) location[2] = pcIndex;
                int slot = pcIndex * Width;
                int kind = records[slot];
                if ((kind == Branch || kind == Conditional || kind == Leave) && !NoteTarget(records, records[slot + 1]))
                    return -1;
                if (kind == Switch)
                {
                    int table = records[slot + 1], count = records[slot + 3];
                    for (int i = 0; i < count; i++)
                        if (!NoteTarget(records, SwitchTarget(records, table, i))) return -1;
                }
            }
            FuseUnsignedSingle(records, length);
            MarkNativeZero(records, length);
            return 0;
        }

        private static bool NoteTarget(int[] records, int offset)
        {
            int slot = offset * Width;
            int kind = records[slot];
            if (kind == MethodSpecCall)
                records[slot + 3] = records[slot + 3] | 1;
            else if (kind == ConvertFloat || kind == ConvertInteger || kind == Load)
                records[slot + 3] = 1;
            return kind != 0 && kind != FilterSkip;
        }

        private static void MarkNativeZero(int[] records, int length)
        {
            for (int pc = 0; pc < length;)
            {
                int slot = pc * Width;
                int next = records[slot + 2];
                if (records[slot] == Constant && records[slot + 1] == 0 && next < length)
                {
                    int conversion = next * Width;
                    int opcode = records[conversion + 1];
                    // A jump directly to the conversion could bring a
                    // different value. Only certify the literal's own path.
                    if (records[conversion] == ConvertInteger && (opcode == 0xd3 || opcode == 0xe0) &&
                        records[conversion + 3] == 0)
                        records[conversion + 3] = ProvenNativeZero;
                }
                pc = next;
            }
        }

        private static void FuseUnsignedSingle(int[] records, int length)
        {
            for (int pc = 0; pc < length;)
            {
                int slot = pc * Width;
                int next = records[slot + 2];
                // .NET 10 rounds adjacent conv.r.un + conv.r4 once. Keep a
                // separately reachable conv.r4 and an explicit conv.r8 intact.
                if (records[slot] == ConvertFloat && records[slot + 1] == 0x76 && next < length)
                {
                    int following = next * Width;
                    if (records[following] == ConvertFloat && records[following + 1] == 0x6b && records[following + 3] == 0)
                    {
                        records[slot + 1] = UnsignedSingle;
                        records[slot + 2] = records[following + 2];
                        records[following] = 0;
                    }
                }
                pc = records[slot + 2];
            }
        }

        internal static int SwitchTarget(int[] records, int table, int index) =>
            records[(table + index * 4) * Width + 1];

        // Only the left operand determines shift width. CLI shift counts are i32.
        internal static bool IsShift(int instruction) => instruction >= 0x74 && instruction <= 0x76;

        internal static bool IsComparison(int instruction) => instruction >= 0x46 && instruction <= 0x4f;

        internal static bool IsIntegerDivision(int instruction, int type) =>
            instruction >= 0x6d && instruction <= 0x70 && SignatureTypes.IsInteger(type);

        internal static bool IsSignedIntegerDivision(int instruction, int type) =>
            (instruction == 0x6d || instruction == 0x6f) && SignatureTypes.IsInteger(type);


        private static int SignedByte(int value) => (value ^ 128) - 128;

        private static int Comparison(int branch)
        {
            if (branch == 1) return 0x45; // brfalse
            if (branch == 2) return 0; // brtrue needs no extra instruction
            if (branch == 3) return 0x46;
            if (branch == 4) return 0x4e;
            if (branch == 5) return 0x4a;
            if (branch == 6) return 0x4c;
            if (branch == 7) return 0x48;
            if (branch == 8) return 0x47;
            if (branch == 9) return 0x4f;
            if (branch == 10) return 0x4b;
            if (branch == 11) return 0x4d;
            return 0x49;
        }
    }
}
