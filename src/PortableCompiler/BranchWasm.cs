using static PortableCompiler.BranchIl;

namespace PortableCompiler
{
    internal static class BranchWasm
    {
        // Each reachable instruction is an arm of the loop. Live eval-stack
        // entries use typed spill locals shared by both sides of every edge.
        internal static int Emit(int[] records, int[] depths, int arguments, int locals, int stack, byte[] code)
        {
            int used = 0;
            int spills = arguments + locals;
            int pcLocal = spills + stack;
            code[used++] = 1;
            used = WasmBinary.Unsigned(code, used, locals + stack + 1);
            code[used++] = 0x7f;
            code[used++] = 0x03; // loop
            code[used++] = 0x40;
            for (int pc = 0; pc < depths.Length; pc++)
            {
                if (depths[pc] == 0)
                    continue;
                int slot = pc * Width;
                int kind = records[slot], operand = records[slot + 1], next = records[slot + 2];
                int top = spills + depths[pc] - 2;
                used = Local(code, used, 0x20, pcLocal);
                used = Const(code, used, pc);
                code[used++] = 0x46;
                code[used++] = 0x04; // if
                code[used++] = 0x40;

                if (kind == Load || kind == Constant || kind == Dup)
                {
                    if (kind == Load) used = Local(code, used, 0x20, operand);
                    else if (kind == Dup) used = Local(code, used, 0x20, top);
                    else used = Const(code, used, operand);
                    used = Local(code, used, 0x21, top + 1);
                }
                else if (kind == Store)
                {
                    used = Local(code, used, 0x20, top);
                    used = Local(code, used, 0x21, operand);
                }
                else if (kind == Unary)
                {
                    used = Local(code, used, 0x20, top);
                    used = Const(code, used, -1);
                    code[used++] = (byte)operand;
                    used = Local(code, used, 0x21, top);
                }
                else if (kind == Binary)
                {
                    used = Local(code, used, 0x20, top - 1);
                    used = Local(code, used, 0x20, top);
                    code[used++] = (byte)operand;
                    used = Local(code, used, 0x21, top - 1);
                }

                if (kind == Return)
                {
                    used = Local(code, used, 0x20, top);
                    code[used++] = 0x0f;
                }
                else
                {
                    if (kind == Conditional)
                    {
                        int comparison = records[slot + 3];
                        // select consumes its condition last and chooses the first
                        // value when the IL branch condition is true.
                        used = Const(code, used, operand);
                        used = Const(code, used, next);
                        if (comparison != 0 && comparison != 0x45)
                            used = Local(code, used, 0x20, top - 1);
                        used = Local(code, used, 0x20, top);
                        if (comparison != 0) code[used++] = (byte)comparison;
                        code[used++] = 0x1b;
                    }
                    else
                    {
                        used = Const(code, used, kind == Branch ? operand : next);
                    }
                    used = Local(code, used, 0x21, pcLocal);
                    code[used++] = 0x0c;
                    code[used++] = 1;
                }
                code[used++] = 0x0b;
            }
            code[used++] = 0x00; // invalid dispatcher address
            code[used++] = 0x0b; // loop end
            code[used++] = 0x00; // loop never returns without a result
            code[used++] = 0x0b; // body end
            return used;
        }

        private static int Const(byte[] code, int used, int value)
        {
            code[used++] = 0x41;
            return WasmBinary.Signed(code, used, value);
        }

        private static int Local(byte[] code, int used, byte instruction, int local)
        {
            code[used++] = instruction;
            return WasmBinary.Unsigned(code, used, local);
        }
    }
}
