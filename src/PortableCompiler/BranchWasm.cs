using static PortableCompiler.BranchIl;
using static PortableCompiler.SignatureTypes;

namespace PortableCompiler
{
    internal static class BranchWasm
    {
        internal static int Emit(int[] records, int[] depths, byte[] states, int[] frameShape, byte[] frame,
            byte[] signatures, int[] offsets, int[] arities, byte[] code)
        {
            int arguments = frameShape[0], locals = frameShape[1], maximum = frameShape[2];
            byte result = (byte)frameShape[3];
            int used = WasmBinary.Unsigned(code, 0, locals + (maximum == 0 ? 1 : 4));
            int spills = arguments + locals;
            int pcLocal = spills + 3 * maximum;
            // Keep declared locals in CLI order, then allocate one bank per spill type.
            for (int i = arguments; i < spills; i++)
            {
                code[used++] = 1;
                used = WasmBinary.ValueType(code, used, frame[i]);
            }
            if (maximum != 0)
            {
                for (byte kind = I32; kind <= Ints; kind++)
                {
                    used = WasmBinary.Unsigned(code, used, maximum);
                    used = WasmBinary.ValueType(code, used, kind);
                }
            }
            code[used++] = 1;
            code[used++] = 0x7f;
            code[used++] = 0x03;
            code[used++] = 0x40;
            for (int pc = 0; pc < depths.Length; pc++)
            {
                if (depths[pc] == 0) continue;
                int slot = pc * Width;
                int kind = records[slot], operand = records[slot + 1], next = records[slot + 2];
                int depth = depths[pc] - 1;
                int state = pc * maximum;
                int top = depth - 1;
                used = Local(code, used, 0x20, pcLocal);
                used = Const(code, used, pc);
                code[used++] = 0x46;
                code[used++] = 0x04;
                code[used++] = 0x40;

                if (kind == Load || kind == Constant || kind == Dup)
                {
                    byte pushed = I32;
                    if (kind == Load)
                    {
                        used = Local(code, used, 0x20, operand);
                        pushed = frame[operand];
                    }
                    else if (kind == Dup)
                    {
                        pushed = states[state + top];
                        used = Local(code, used, 0x20, Spill(spills, maximum, pushed, top));
                    }
                    else used = Const(code, used, operand);
                    used = Local(code, used, 0x21, Spill(spills, maximum, pushed, depth));
                }
                else if (kind == Store)
                {
                    used = Local(code, used, 0x20, Spill(spills, maximum, frame[operand], top));
                    used = Local(code, used, 0x21, operand);
                }
                else if (kind == Unary || kind == Narrow)
                {
                    used = Local(code, used, 0x20, spills + top);
                    used = Const(code, used, kind == Narrow ? 255 : -1);
                    code[used++] = kind == Narrow ? (byte)0x71 : (byte)operand;
                    used = Local(code, used, 0x21, spills + top);
                }
                else if (kind == Binary)
                {
                    used = Local(code, used, 0x20, spills + top - 1);
                    used = Local(code, used, 0x20, spills + top);
                    code[used++] = (byte)operand;
                    used = Local(code, used, 0x21, spills + top - 1);
                }
                else if (kind == Call)
                {
                    int arity = arities[operand];
                    int first = depth - arity;
                    for (int i = 0; i < arity; i++)
                        used = Local(code, used, 0x20, Spill(spills, maximum, states[state + first + i], first + i));
                    code[used++] = 0x10;
                    used = WasmBinary.Unsigned(code, used, operand);
                    byte returned = signatures[offsets[operand]];
                    if (returned != Void)
                        used = Local(code, used, 0x21, Spill(spills, maximum, returned, first));
                }
                else if (kind == NewArray)
                {
                    used = Local(code, used, 0x20, spills + top);
                    used = Gc(code, used, 7, operand);
                    used = Local(code, used, 0x21, Spill(spills, maximum, (byte)operand, top));
                }
                else if (kind == ArrayLength)
                {
                    used = Local(code, used, 0x20, Spill(spills, maximum, states[state + top], top));
                    code[used++] = 0xfb;
                    code[used++] = 0x0f;
                    used = Local(code, used, 0x21, spills + top);
                }
                else if (kind == ArrayGet)
                {
                    byte array = states[state + top - 1];
                    used = Local(code, used, 0x20, Spill(spills, maximum, array, top - 1));
                    used = Local(code, used, 0x20, spills + top);
                    int instruction = operand == 0x94 ? 0x0b : operand == 0x90 ? 0x0c : 0x0d;
                    used = Gc(code, used, instruction, array);
                    used = Local(code, used, 0x21, spills + top - 1);
                }
                else if (kind == ArraySet)
                {
                    byte array = states[state + top - 2];
                    used = Local(code, used, 0x20, Spill(spills, maximum, array, top - 2));
                    used = Local(code, used, 0x20, spills + top - 1);
                    used = Local(code, used, 0x20, spills + top);
                    used = Gc(code, used, 0x0e, array);
                }

                if (kind == Return)
                {
                    if (result != Void)
                        used = Local(code, used, 0x20, Spill(spills, maximum, result, top));
                    code[used++] = 0x0f;
                }
                else
                {
                    if (kind == Conditional)
                    {
                        int comparison = records[slot + 3];
                        used = Const(code, used, operand);
                        used = Const(code, used, next);
                        if (comparison != 0 && comparison != 0x45)
                            used = Local(code, used, 0x20, spills + top - 1);
                        used = Local(code, used, 0x20, spills + top);
                        if (comparison != 0) code[used++] = (byte)comparison;
                        code[used++] = 0x1b;
                    }
                    else used = Const(code, used, kind == Branch ? operand : next);
                    used = Local(code, used, 0x21, pcLocal);
                    code[used++] = 0x0c;
                    code[used++] = 1;
                }
                code[used++] = 0x0b;
            }
            code[used++] = 0;
            code[used++] = 0x0b;
            code[used++] = 0;
            code[used++] = 0x0b;
            return used;
        }

        private static int Spill(int first, int maximum, byte type, int depth) =>
            first + (type - 1) * maximum + depth;

        private static int Gc(byte[] code, int used, int instruction, int array)
        {
            code[used++] = 0xfb;
            code[used++] = (byte)instruction;
            code[used++] = (byte)(array - Bytes);
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
