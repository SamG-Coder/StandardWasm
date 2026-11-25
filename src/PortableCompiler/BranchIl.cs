using PeLibrary;

namespace PortableCompiler
{
    internal static class BranchIl
    {
        internal const int Width = 4;
        internal const int Load = 1, Store = 2, Constant = 3, Unary = 4, Binary = 5,
            Return = 6, Branch = 7, Conditional = 8, Nop = 9, Dup = 10, Pop = 11, Call = 12, NewArray = 13, ArrayLength = 14, ArrayGet = 15, ArraySet = 16, Narrow = 17, NewObject = 18, GetField = 19, SetField = 20, BaseCtor = 21, LoadFunction = 22, NewDelegate = 23, InvokeDelegate = 24, MemberCall = 25, LoadString = 26, StringLength = 27, StringCharacter = 28, JoinStrings = 29, StringFromChars = 30, NullString = 31, WideConstant = 32, ConvertInteger = 33, Switch = 34;

        // Records are keyed by IL byte offset. Operand bytes retain kind zero.
        // Record fields are kind, operand, next IL offset and auxiliary operand (such as a comparison or high literal word).
        internal static int Decode(byte[] image, int start, int length, int arguments, int locals, int[] records, int[] location)
        {
            int pc = 0;
            while (pc < length)
            {
                if (location.Length != 0) location[2] = pc;
                int slot = pc * Width;
                int op = image[start + pc++];
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
                else if (op == 0x10)
                {
                    kind = Store;
                    if (pc >= length) return -1;
                    operand = image[start + pc++];
                    if (operand >= arguments) return -1;
                }
                else if (op >= 6 && op <= 13 || op == 0x11 || op == 0x13)
                {
                    bool store = op == 0x13 || (op >= 10 && op <= 13);
                    kind = store ? Store : Load;
                    if (op == 0x11 || op == 0x13)
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
                else if (op == 0x28 || op == 0x6f || op == 0x73 || op == 0x7b || op == 0x7d)
                {
                    if (length - pc < 4) return -1;
                    uint token = ImageBytes.U32(image, (uint)(start + pc));
                    pc += 4;
                    uint table = token >> 24;
                    operand = (int)(token & 0xffffff);
                    if (operand == 0) return -1;
                    if (op == 0x7b || op == 0x7d)
                    {
                        if (table != 4) return -2;
                        kind = op == 0x7b ? GetField : SetField;
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
                else if (op == 0x14) kind = NullString;
                else if (op == 0x72)
                {
                    if (length - pc < 4) return -1;
                    uint token = ImageBytes.U32(image, (uint)(start + pc));
                    pc += 4;
                    if ((token >> 24) != 0x70) return -1;
                    kind = LoadString;
                    operand = (int)(token & 0xffffff);
                }
                else if (op == 0x8d)
                {
                    kind = NewArray;
                    if (length - pc < 4) return -1;
                    operand = unchecked((int)ImageBytes.U32(image, (uint)(start + pc)));
                    pc += 4;
                }
                else if (op == 0x8e) kind = ArrayLength;
                else if (op == 0x90 || op == 0x91 || op == 0x93 || op == 0x94)
                {
                    kind = ArrayGet;
                    operand = op;
                }
                else if (op == 0x9c || op == 0x9d || op == 0x9e)
                {
                    kind = ArraySet;
                    operand = op;
                }
                else if (op == 0xd2 || op == 0xd1)
                {
                    kind = Narrow;
                    operand = op == 0xd2 ? 255 : 65535;
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
                    else
                    {
                        if (extended < 1 || extended > 5) return -2;
                        kind = Binary;
                        operand = extended == 1 ? 0x46 : Comparison(extended == 2 ? 5 :
                            extended == 3 ? 10 : extended == 4 ? 7 : 12);
                    }
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
                if ((kind == Branch || kind == Conditional) && records[records[slot + 1] * Width] == 0)
                    return -1;
                if (kind == Switch)
                {
                    int table = records[slot + 1], count = records[slot + 3];
                    for (int i = 0; i < count; i++)
                        if (records[SwitchTarget(records, table, i) * Width] == 0)
                            return -1;
                }
            }
            return 0;
        }

        internal static int SwitchTarget(int[] records, int table, int index) =>
            records[(table + index * 4) * Width + 1];

        // Only the left operand determines shift width. CLI shift counts are i32.
        internal static bool IsShift(int instruction) => instruction >= 0x74 && instruction <= 0x76;

        internal static bool IsComparison(int instruction) => instruction >= 0x46 && instruction <= 0x4f;

        internal static bool ConvertsToWide(int instruction) => instruction == 0x6a || instruction == 0x6e;

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
