using static PortableCompiler.BranchIl;
using static PeLibrary.ObjectLayouts;
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
            int classes = frameShape[4];
            bool stringScratch = frameShape[5] != 0;
            int used = WasmBinary.Unsigned(code, 0, locals + classes + (maximum == 0 ? 1 : 1 + I64 + classes) + (stringScratch ? 2 : 0));
            int spills = arguments + locals;
            int temporaries = spills + (I64 + classes) * maximum;
            int scratch = temporaries + classes;
            int pcLocal = scratch + (stringScratch ? 5 : 0);
            // Keep declared locals in CLI order, then allocate one bank per spill type.
            for (int i = arguments; i < spills; i++)
            {
                code[used++] = 1;
                used = WasmBinary.ValueType(code, used, frame[i]);
            }
            if (maximum != 0)
            {
                for (int kind = I32; kind <= I64 + classes; kind++)
                {
                    used = WasmBinary.Unsigned(code, used, maximum);
                    used = WasmBinary.ValueType(code, used, (byte)kind);
                }
            }
            for (int i = 0; i < classes; i++)
            {
                code[used++] = 1;
                used = WasmBinary.ValueType(code, used, (byte)(FirstClass + i));
            }
            if (stringScratch)
            {
                code[used++] = 3;
                used = WasmBinary.ValueType(code, used, Chars);
                code[used++] = 2;
                code[used++] = 0x7f;
            }
            code[used++] = 1;
            code[used++] = 0x7f;
            code[used++] = 0x03;
            code[used++] = 0x40;
            for (int pc = 0; pc < depths.Length; pc++)
            {
                if (depths[pc] == 0) continue;
                int slot = pc * BranchIl.Width;
                int kind = records[slot], operand = records[slot + 1], next = records[slot + 2];
                int depth = depths[pc] - 1;
                int state = pc * maximum;
                int top = depth - 1;
                used = Local(code, used, 0x20, pcLocal);
                used = Const(code, used, pc);
                code[used++] = 0x46;
                code[used++] = 0x04;
                code[used++] = 0x40;

                if (kind == Load || kind == Constant || kind == WideConstant || kind == Dup)
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
                    else if (kind == WideConstant)
                    {
                        pushed = I64;
                        used = WideConst(code, used, operand, records[slot + 3]);
                    }
                    else used = Const(code, used, operand);
                    used = Local(code, used, 0x21, Spill(spills, maximum, pushed, depth));
                }
                else if (kind == LoadString)
                {
                    int characters = records[slot + 3];
                    if (characters != 0)
                    {
                        used = Const(code, used, operand);
                        used = Const(code, used, characters);
                    }
                    code[used++] = 0xfb;
                    code[used++] = characters == 0 ? (byte)8 : (byte)9; // fixed empty or passive data
                    code[used++] = 4;
                    code[used++] = 0;
                    used = StringWasm.Wrap(code, used);
                    used = Local(code, used, 0x21, Spill(spills, maximum, String, depth));
                }
                else if (kind == NullString)
                {
                    code[used++] = 0xd0;
                    code[used++] = 5;
                    used = Local(code, used, 0x21, Spill(spills, maximum, String, depth));
                }
                else if (kind == JoinStrings || kind == StringFromChars)
                {
                    bool join = kind == JoinStrings;
                    int at = top - (join ? 1 : 0);
                    used = StringWasm.Copy(code, used, Spill(spills, maximum, join ? String : Chars, at),
                        Spill(spills, maximum, String, top), scratch, join);
                    used = Local(code, used, 0x21, Spill(spills, maximum, String, at));
                }
                else if (kind == StringLength || kind == StringCharacter)
                {
                    int at = top - (kind == StringCharacter ? 1 : 0);
                    used = Local(code, used, 0x20, Spill(spills, maximum, String, at));
                    used = StringWasm.Unwrap(code, used);
                    if (kind == StringCharacter)
                    {
                        used = Local(code, used, 0x20, spills + top);
                        code[used++] = 0xfb;
                        code[used++] = 0x0d;
                        code[used++] = 4;
                    }
                    else
                    {
                        code[used++] = 0xfb;
                        code[used++] = 0x0f;
                    }
                    used = Local(code, used, 0x21, spills + at);
                }
                else if (kind == LoadFunction)
                {
                    used = Const(code, used, operand);
                    used = Local(code, used, 0x21, Spill(spills, maximum, MethodPointer, depth));
                }
                else if (kind == NewDelegate)
                {
                    byte receiver = (byte)records[slot + 3];
                    used = Local(code, used, 0x20, Spill(spills, maximum, receiver, top - 1));
                    code[used++] = 0xd4;
                    used = Local(code, used, 0x20, Spill(spills, maximum, MethodPointer, top));
                    code[used++] = 0x25; // table.get from the generated thunk table
                    code[used++] = 0;
                    code[used++] = 0xfb; // struct.new closure
                    code[used++] = 0;
                    code[used++] = 3;
                    used = Local(code, used, 0x21, Spill(spills, maximum, Delegate, top - 1));
                }
                else if (kind == InvokeDelegate)
                {
                    int closure = Spill(spills, maximum, Delegate, top);
                    used = Local(code, used, 0x20, closure);
                    code[used++] = 0xfb;
                    code[used++] = 2; // struct.get receiver
                    code[used++] = 3;
                    code[used++] = 0;
                    used = Local(code, used, 0x20, closure);
                    code[used++] = 0xfb;
                    code[used++] = 2; // struct.get callable reference
                    code[used++] = 3;
                    code[used++] = 1;
                    code[used++] = 0xfb; // ref.cast to the shared thunk signature
                    code[used++] = 22;
                    code[used++] = 2;
                    code[used++] = 0x14; // call_ref
                    code[used++] = 2;
                    used = Local(code, used, 0x21, spills + top);
                }
                else if (kind == Store)
                {
                    used = Local(code, used, 0x20, Spill(spills, maximum, frame[operand], top));
                    used = Local(code, used, 0x21, operand);
                }
                else if (kind == Unary || kind == Narrow || kind == ConvertInteger)
                {
                    byte source = states[state + top];
                    byte target = kind == Unary ? source : kind == ConvertInteger && ConvertsToWide(operand) ? I64 : I32;
                    used = Local(code, used, 0x20, Spill(spills, maximum, source, top));
                    if (source != target)
                        code[used++] = target == I32 ? (byte)0xa7 : operand == 0x6a ? (byte)0xac : (byte)0xad;
                    if (kind == Narrow)
                    {
                        used = Const(code, used, operand);
                        code[used++] = 0x71; // i32.and
                    }
                    else if (kind == Unary)
                    {
                        used = source == I64 ? WideConst(code, used, -1, -1) : Const(code, used, -1);
                        code[used++] = NumericOpcode(operand, source);
                    }
                    used = Local(code, used, 0x21, Spill(spills, maximum, target, top));
                }
                else if (kind == Binary)
                {
                    byte type = states[state + top - 1];
                    byte resultType = IsComparison(operand) ? I32 : type;
                    int left = Spill(spills, maximum, type, top - 1);
                    int right = Spill(spills, maximum, states[state + top], top);
                    if (operand == 0x6f)
                        used = GuardRemainder(code, used, type, left, right);
                    used = Local(code, used, 0x20, left);
                    used = Local(code, used, 0x20, right);
                    if (type == I64 && IsShift(operand)) code[used++] = 0xad;
                    code[used++] = NumericOpcode(operand, type);
                    used = Local(code, used, 0x21, Spill(spills, maximum, resultType, top - 1));
                }
                else if (kind == Call || kind == NewObject)
                {
                    int arity = arities[operand], shape = offsets[operand];
                    int receiver = kind == NewObject ? 1 : 0;
                    int first = depth - arity + receiver;
                    int temp = 0;
                    if (kind == NewObject)
                    {
                        byte type = signatures[shape + 1];
                        temp = temporaries + type - FirstClass;
                        used = Gc(code, used, 1, type); // struct.new_default
                        used = Local(code, used, 0x22, temp);
                    }
                    for (int i = receiver; i < arity; i++)
                    {
                        int argument = first + i - receiver;
                        used = Local(code, used, 0x20, Spill(spills, maximum, states[state + argument], argument));
                        if (i == 0 && records[slot + 3] != 0)
                            code[used++] = 0xd4; // callvirt null-check on a known nonvirtual target
                    }
                    code[used++] = 0x10;
                    used = WasmBinary.Unsigned(code, used, operand);
                    byte returned = signatures[shape];
                    if (kind == NewObject)
                    {
                        returned = signatures[shape + 1];
                        used = Local(code, used, 0x20, temp);
                    }
                    if (returned != Void)
                        used = Local(code, used, 0x21, Spill(spills, maximum, returned, first));
                }
                else if (kind == GetField || kind == SetField)
                {
                    int descriptor = records[slot + 3];
                    byte type = (byte)(descriptor >> 8), value = unchecked((byte)descriptor);
                    int address = kind == GetField ? top : top - 1;
                    used = Local(code, used, 0x20, Spill(spills, maximum, type, address));
                    if (kind == SetField) used = Local(code, used, 0x20, Spill(spills, maximum, value, top));
                    used = Gc(code, used, kind == GetField ? 2 : 5, type);
                    used = WasmBinary.Unsigned(code, used, operand);
                    if (kind == GetField) used = Local(code, used, 0x21, Spill(spills, maximum, value, top));
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
                        byte type = states[state + top];
                        if (IsComparison(comparison))
                            used = Local(code, used, 0x20, Spill(spills, maximum, type, top - 1));
                        used = Local(code, used, 0x20, Spill(spills, maximum, type, top));
                        if (IsComparison(comparison)) code[used++] = NumericOpcode(comparison, type);
                        else if (type == I64)
                        {
                            code[used++] = 0x50; // i64.eqz produces the i32 select condition
                            if (comparison == 0) code[used++] = 0x45;
                        }
                        else if (comparison != 0) code[used++] = 0x45;
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

        private static byte NumericOpcode(int instruction, byte type) =>
            (byte)(type == I32 ? instruction : instruction + (IsComparison(instruction) ? 0x0b : 0x12));

        // The CLR throws for MinValue % -1 even though Wasm rem_s returns zero.
        // Keep the failure observable as a Wasm trap until typed exceptions are available.
        private static int GuardRemainder(byte[] code, int used, byte type, int left, int right)
        {
            used = Local(code, used, 0x20, left);
            used = type == I64 ? WideConst(code, used, 0, int.MinValue) : Const(code, used, int.MinValue);
            code[used++] = NumericOpcode(0x46, type);
            used = Local(code, used, 0x20, right);
            used = type == I64 ? WideConst(code, used, -1, -1) : Const(code, used, -1);
            code[used++] = NumericOpcode(0x46, type);
            code[used++] = 0x71; // i32.and
            code[used++] = 0x04; // if
            code[used++] = 0x40; // void
            code[used++] = 0; // unreachable
            code[used++] = 0x0b;
            return used;
        }

        private static int Spill(int first, int maximum, byte type, int depth) =>
            first + (type - 1) * maximum + depth;

        private static int Gc(byte[] code, int used, int instruction, int array)
        {
            code[used++] = 0xfb;
            code[used++] = (byte)instruction;
            return WasmBinary.Unsigned(code, used, WasmBinary.HeapType((byte)array));
        }

        private static int Const(byte[] code, int used, int value)
        {
            code[used++] = 0x41;
            return WasmBinary.Signed(code, used, value);
        }

        private static int WideConst(byte[] code, int used, int low, int high)
        {
            code[used++] = 0x42;
            return WasmBinary.Signed64(code, used, low, high);
        }

        private static int Local(byte[] code, int used, byte instruction, int local)
        {
            code[used++] = instruction;
            return WasmBinary.Unsigned(code, used, local);
        }
    }
}
