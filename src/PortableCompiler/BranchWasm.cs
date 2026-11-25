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
            int spillKinds = FirstClass - 1 + classes;
            int used = WasmBinary.Unsigned(code, 0, locals + classes + (maximum == 0 ? 1 : 1 + spillKinds) + (stringScratch ? 2 : 0));
            int spills = arguments + locals;
            int temporaries = spills + spillKinds * maximum;
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
                for (int kind = I32; kind <= spillKinds; kind++)
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

                if (kind == Load || kind == Constant || kind == WideConstant || kind == Dup ||
                    kind == SingleConstant || kind == DoubleConstant)
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
                    else if (kind == SingleConstant || kind == DoubleConstant)
                    {
                        pushed = kind == SingleConstant ? F32 : F64;
                        used = FloatWasm.Literal(code, used, pushed, operand, records[slot + 3]);
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
                else if (kind == LoadEventArgsEmpty || kind == NewEventArgs)
                {
                    if (kind == LoadEventArgsEmpty)
                    {
                        code[used++] = 0x23; // global.get EventArgs.Empty
                        code[used++] = 0;
                    }
                    else used = Gc(code, used, 1, EventArgsReference); // struct.new_default
                    used = Local(code, used, 0x21, Spill(spills, maximum, EventArgsReference, depth));
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
                    byte closureType = (byte)operand;
                    used = Local(code, used, 0x20, Spill(spills, maximum, receiver, top - 1));
                    code[used++] = 0xd4;
                    used = Local(code, used, 0x20, Spill(spills, maximum, MethodPointer, top));
                    code[used++] = 0x25; // table.get from the generated thunk table
                    code[used++] = 0;
                    if (closureType == EventHandlerReference)
                        used = CastThunk(code, used, WasmBinary.EventThunkType);
                    used = Gc(code, used, 0, closureType); // struct.new closure
                    used = Local(code, used, 0x21, Spill(spills, maximum, closureType, top - 1));
                }
                else if (kind == InvokeDelegate)
                {
                    byte closureType = (byte)operand;
                    bool eventHandler = closureType == EventHandlerReference;
                    int closure = Spill(spills, maximum, closureType, top - (eventHandler ? 2 : 0));
                    used = Local(code, used, 0x20, closure);
                    used = Gc(code, used, 2, closureType); // struct.get receiver
                    code[used++] = 0;
                    if (eventHandler)
                    {
                        used = Local(code, used, 0x20, Spill(spills, maximum, states[state + top - 1], top - 1));
                        used = Local(code, used, 0x20, Spill(spills, maximum, EventArgsReference, top));
                    }
                    used = Local(code, used, 0x20, closure);
                    used = Gc(code, used, 2, closureType); // struct.get callable reference
                    code[used++] = 1;
                    if (!eventHandler)
                        used = CastThunk(code, used, WasmBinary.ScalarThunkType);
                    code[used++] = 0x14; // call_ref
                    code[used++] = eventHandler ? (byte)WasmBinary.EventThunkType : (byte)WasmBinary.ScalarThunkType;
                    if (!eventHandler)
                        used = Local(code, used, 0x21, spills + top);
                }
                else if (kind == Store)
                {
                    used = Local(code, used, 0x20, Spill(spills, maximum, states[state + top], top));
                    used = Local(code, used, 0x21, operand);
                }
                else if (kind == Unary || kind == Narrow || kind == ConvertInteger || kind == ConvertFloat)
                {
                    byte source = states[state + top];
                    byte target = kind == Unary ? source : kind == ConvertFloat ? FloatWasm.Target(operand) :
                        kind == ConvertInteger && ConvertsToWide(operand) ? I64 : I32;
                    used = Local(code, used, 0x20, Spill(spills, maximum, source, top));
                    if (kind == Unary && IsFloat(source))
                        code[used++] = source == F32 ? (byte)0x8c : (byte)0x9a;
                    else if (kind == ConvertFloat || IsFloat(source))
                        used = FloatWasm.Convert(code, used, operand, source, target);
                    else if (source != target)
                        code[used++] = target == I32 ? (byte)0xa7 : operand == 0x6a ? (byte)0xac : (byte)0xad;
                    if (kind == Narrow)
                    {
                        used = Const(code, used, operand);
                        code[used++] = 0x71; // i32.and
                    }
                    else if (kind == Unary && !IsFloat(source))
                    {
                        used = source == I64 ? WideConst(code, used, -1, -1) : Const(code, used, -1);
                        code[used++] = NumericOpcode(operand, source);
                    }
                    used = Local(code, used, 0x21, Spill(spills, maximum, target, top));
                }
                else if (kind == Binary)
                {
                    byte type = states[state + top - 1];
                    byte rightType = states[state + top];
                    byte width = IsFloat(type) ? FloatWidth(type, rightType) : type;
                    byte resultType = IsComparison(operand) ? I32 : width;
                    int left = Spill(spills, maximum, type, top - 1);
                    int right = Spill(spills, maximum, states[state + top], top);
                    if (operand == 0x6f)
                        used = GuardRemainder(code, used, type, left, right);
                    used = NumericLocal(code, used, left, type, width);
                    used = NumericLocal(code, used, right, rightType, width);
                    if (type == I64 && IsShift(operand)) code[used++] = 0xad;
                    if (IsFloat(width)) used = FloatWasm.Binary(code, used, operand, width);
                    else if (IsObjectReference(type)) code[used++] = 0xd3; // ref.eq
                    else code[used++] = NumericOpcode(operand, type);
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
                    if (kind == SetField)
                        used = Local(code, used, 0x20, Spill(spills, maximum, states[state + top], top));
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
                    // Unpacked integer arrays use array.get. Packed elements require extension.
                    int instruction = array == Ints || array == Longs ? 0x0b : operand == 0x90 ? 0x0c : 0x0d;
                    used = Gc(code, used, instruction, array);
                    used = Local(code, used, 0x21, Spill(spills, maximum, ArrayValueType(array), top - 1));
                }
                else if (kind == ArraySet)
                {
                    byte array = states[state + top - 2];
                    used = Local(code, used, 0x20, Spill(spills, maximum, array, top - 2));
                    used = Local(code, used, 0x20, spills + top - 1);
                    used = Local(code, used, 0x20, Spill(spills, maximum, ArrayValueType(array), top));
                    used = Gc(code, used, 0x0e, array);
                }

                if (kind == Return)
                {
                    if (result != Void)
                        used = Local(code, used, 0x20, Spill(spills, maximum, states[state + top], top));
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
                        byte width = type;
                        if (IsComparison(comparison))
                        {
                            byte leftType = states[state + top - 1];
                            if (IsFloat(type)) width = FloatWidth(leftType, type);
                            used = NumericLocal(code, used, Spill(spills, maximum, leftType, top - 1), leftType, width);
                        }
                        used = NumericLocal(code, used, Spill(spills, maximum, type, top), type, width);
                        if (IsFloat(width)) used = FloatWasm.Binary(code, used, comparison, width);
                        else if (IsObjectReference(type))
                        {
                            code[used++] = IsComparison(comparison) ? (byte)0xd3 : (byte)0xd1; // ref.eq or ref.is_null
                            if (comparison == 0 || comparison == 0x47) code[used++] = 0x45;
                        }
                        else if (IsComparison(comparison)) code[used++] = NumericOpcode(comparison, type);
                        else if (type == I64)
                        {
                            code[used++] = 0x50; // i64.eqz produces the i32 select condition
                            if (comparison == 0) code[used++] = 0x45;
                        }
                        else if (comparison != 0) code[used++] = 0x45;
                        code[used++] = 0x1b;
                    }
                    else if (kind == Switch)
                        used = SwitchDispatch(code, used, records, operand, records[slot + 3], next, spills + top);
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

        private static int SwitchDispatch(byte[] code, int used, int[] records, int table, int count,
            int fallthrough, int selector)
        {
            // The outer block yields the next IL offset. The inner empty blocks
            // give br_table a label for each case and the default in CLI order.
            code[used++] = 0x02; // block
            code[used++] = 0x7f; // i32 result
            for (int i = 0; i <= count; i++)
            {
                code[used++] = 0x02;
                code[used++] = 0x40; // void
            }
            used = Local(code, used, 0x20, selector);
            code[used++] = 0x0e; // br_table interprets the selector as unsigned
            used = WasmBinary.Unsigned(code, used, count);
            for (int i = 0; i <= count; i++)
                used = WasmBinary.Unsigned(code, used, i);

            for (int i = 0; i < count; i++)
            {
                code[used++] = 0x0b; // end the block selected by case i
                used = Const(code, used, SwitchTarget(records, table, i));
                code[used++] = 0x0c; // branch to the result block
                used = WasmBinary.Unsigned(code, used, count - i);
            }
            code[used++] = 0x0b; // end the default block
            used = Const(code, used, fallthrough);
            code[used++] = 0x0b; // end the result block
            return used;
        }

        private static int NumericLocal(byte[] code, int used, int local, byte source, byte width)
        {
            used = Local(code, used, 0x20, local);
            if (source == F32 && width == F64) code[used++] = 0xbb;
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

        private static int CastThunk(byte[] code, int used, byte type)
        {
            code[used++] = 0xfb;
            code[used++] = 22; // ref.cast from the shared table's funcref
            code[used++] = type;
            return used;
        }

        private static int Gc(byte[] code, int used, int instruction, int type)
        {
            code[used++] = 0xfb;
            code[used++] = (byte)instruction;
            return WasmBinary.Unsigned(code, used, WasmBinary.HeapType((byte)type));
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
