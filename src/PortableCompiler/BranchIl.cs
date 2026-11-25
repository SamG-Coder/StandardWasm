using PeLibrary;

namespace PortableCompiler
{
    internal static class BranchIl
    {
        internal const int Width = 4;
        internal const int Load = 1, Store = 2, Constant = 3, Unary = 4, Binary = 5,
            Return = 6, Branch = 7, Conditional = 8, Nop = 9, Dup = 10, Pop = 11;

        // Records are keyed by IL byte offset. Operand bytes retain kind zero.
        // Record fields are kind, operand, next IL offset and branch comparison.
        internal static int Decode(byte[] image, int start, int length, int arguments, int locals, int[] records)
        {
            int pc = 0;
            while (pc < length)
            {
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
                else if (op == 0xfe)
                {
                    if (pc >= length) return -1;
                    int extended = image[start + pc++];
                    if (extended < 1 || extended > 5) return -2;
                    kind = Binary;
                    operand = extended == 1 ? 0x46 : Comparison(extended == 2 ? 5 :
                        extended == 3 ? 10 : extended == 4 ? 7 : 12);
                }
                else if (op == 0x65 || op == 0x66)
                {
                    kind = Unary;
                    operand = op == 0x65 ? 0x6c : 0x73;
                }
                else if (op == 0 || op == 0x69 || op == 0x6d)
                {
                    kind = Nop;
                    operand = op; // conv.i4 and conv.u4 require a live i32 at the top.
                }
                else if (op == 0x2a) kind = Return;
                else if (op == 0x25) kind = Dup;
                else if (op == 0x26) kind = Pop;
                else
                {
                    kind = Binary;
                    operand = StraightLineIl.Binary(op);
                    if (operand < 0) return -2;
                }
                records[slot] = kind;
                records[slot + 1] = operand;
                records[slot + 2] = pc;
                records[slot + 3] = comparison;
            }

            for (int pcIndex = 0; pcIndex < length; pcIndex += records[pcIndex * Width + 2] - pcIndex)
            {
                int slot = pcIndex * Width;
                int kind = records[slot];
                if ((kind == Branch || kind == Conditional) && records[records[slot + 1] * Width] == 0)
                    return -1;
            }
            return 0;
        }

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
