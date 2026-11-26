using static PortableCompiler.BranchIl;
using static PeLibrary.ObjectLayouts;
using static PortableCompiler.SignatureTypes;

namespace PortableCompiler
{
    internal static class BranchWasm
    {
        internal static int Emit(int[] records, int[] depths, byte[] states, int[] frameShape, int[] frame,
            int[] signatures, int[] offsets, int[] arities, byte[] code, int[] remainderFixup, int[] catchRegion,
            byte[] outgoingNulls, int[] objects)
        {
            int arguments = frameShape[0], locals = frameShape[1], maximum = frameShape[2];
            int result = frameShape[3];
            int classes = frameShape[4];
            bool stringScratch = frameShape[5] != 0;
            bool eventScratch = frameShape[6] != 0;
            int exceptionLocals = frameShape[8], continuations = frameShape[9];
            int hostImports = frameShape[10];
            bool hasNull = outgoingNulls.Length != 0;
            int spillKinds = FirstClass - 1 + classes;
            int used = WasmBinary.Unsigned(code, 0, locals + classes + (hasNull ? 1 : 0) + (maximum == 0 ? 1 : 1 + spillKinds) +
                (stringScratch ? 2 : 0) + (eventScratch ? 2 : 0) + (exceptionLocals != 0 ? 1 : 0) +
                (continuations != 0 ? 1 : 0));
            int spills = arguments + locals + (hasNull ? 1 : 0);
            int temporaries = spills + spillKinds * maximum;
            int scratch = temporaries + classes;
            int eventLocals = scratch + (stringScratch ? 5 : 0);
            int pcLocal = eventLocals + (eventScratch ? 6 : 0);
            int continuationBase = pcLocal + 1 + exceptionLocals;
            // Keep declared locals in CLI order, then allocate one bank per spill type.
            for (int i = arguments; i < arguments + locals; i++)
            {
                code[used++] = 1;
                used = WasmBinary.ValueType(code, used, Representation(objects, frame[i]));
            }
            if (hasNull)
            {
                // A default-initialized bottom reference supplies any concrete
                // managed heap type and never needs to be written.
                code[used++] = 1;
                code[used++] = NullSpillWasm.NullRef;
            }
            if (maximum != 0)
            {
                for (int kind = I32; kind <= spillKinds; kind++)
                {
                    used = WasmBinary.Unsigned(code, used, maximum);
                    used = WasmBinary.ValueType(code, used, Representation(objects, kind));
                }
            }
            for (int i = 0; i < classes; i++)
            {
                code[used++] = 1;
                used = WasmBinary.ValueType(code, used, Representation(objects, FirstClass + i));
            }
            if (stringScratch)
            {
                code[used++] = 3;
                used = WasmBinary.ValueType(code, used, Chars);
                code[used++] = 2;
                code[used++] = 0x7f;
            }
            if (eventScratch)
            {
                code[used++] = 5; // loop counters and copy lengths
                code[used++] = 0x7f;
                code[used++] = 1; // unpublished output array
                used = WasmBinary.ValueType(code, used, EventHandlerReference);
            }
            code[used++] = 1;
            code[used++] = 0x7f;
            if (exceptionLocals != 0)
            {
                used = WasmBinary.Unsigned(code, used, exceptionLocals);
                code[used++] = ExceptionDispatchWasm.ExceptionReference;
            }
            if (continuations != 0)
            {
                used = WasmBinary.Unsigned(code, used, continuations);
                code[used++] = 0x7f; // normal cleanup destinations
            }
            // Raw Wasm callers supply i32. Enforce the CLI argument width before
            // the first instruction, including on entry from a managed call.
            for (int i = 0; i < arguments; i++)
            {
                if (!IsNarrow(frame[i])) continue;
                used = Local(code, used, 0x20, i);
                used = NormalizeSlot(code, used, frame[i]);
                used = Local(code, used, 0x21, i);
            }
            code[used++] = 0x03;
            code[used++] = 0x40;
            if (catchRegion.Length != 0) used = ExceptionDispatchWasm.Begin(code, used);
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

                if (kind == Load || kind == Constant || kind == WideConstant || (kind == Dup && states[state + top] != NullValue) ||
                    kind == SingleConstant || kind == DoubleConstant)
                {
                    byte pushed = I32;
                    if (kind == Load)
                    {
                        used = Local(code, used, 0x20, operand);
                        pushed = StackType(frame[operand]);
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
                else if (kind == JoinStrings || kind == StringFromChars)
                {
                    bool join = kind == JoinStrings;
                    int at = top - (join ? 1 : 0);
                    used = StringWasm.Copy(code, used, Spill(spills, maximum, states[state + at], at),
                        Spill(spills, maximum, states[state + top], top), scratch, join);
                    used = Local(code, used, 0x21, Spill(spills, maximum, String, at));
                }
                else if (kind == StringLength || kind == StringCharacter)
                {
                    int at = top - (kind == StringCharacter ? 1 : 0);
                    int reference = Spill(spills, maximum, states[state + at], at);
                    used = GuardNull(code, used, reference);
                    if (kind == StringCharacter)
                        used = GuardArrayIndex(code, used, reference, spills + top, true);
                    used = Local(code, used, 0x20, reference);
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
                else if (kind == ConsoleWriter)
                {
                    used = Const(code, used, operand); // host stream number
                    used = Local(code, used, 0x21, Spill(spills, maximum, PeLibrary.ConsoleStorage.Writer, depth));
                }
                else if (kind == ConsoleWrite)
                {
                    bool text = operand != PeLibrary.ConsoleMetadata.WriteEmptyLine;
                    int writer = text ? top - 1 : top;
                    // The String scratch bank supplies a Char[] and two i32 locals.
                    used = ConsoleWasm.Write(code, used, operand,
                        Spill(spills, maximum, PeLibrary.ConsoleStorage.Writer, writer),
                        text ? Spill(spills, maximum, states[state + top], top) : 0,
                        scratch, scratch + 3, scratch + 4);
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
                    int pointer = Spill(spills, maximum, MethodPointer, top);
                    int destination = Spill(spills, maximum, closureType, top - 1);
                    used = Local(code, used, 0x20, pointer);
                    code[used++] = 0x25; // table.get from the generated thunk table
                    code[used++] = 0;
                    if (closureType == EventHandlerReference)
                    {
                        used = CastThunk(code, used, WasmBinary.EventThunkType);
                        used = EventWasm.NewList(code, used, pointer, destination);
                    }
                    else used = Gc(code, used, 0, closureType); // struct.new scalar closure
                    used = Local(code, used, 0x21, destination);
                }
                else if (kind == CombineHandler || kind == RemoveHandler)
                {
                    int left = Spill(spills, maximum, states[state + top - 1], top - 1);
                    int right = Spill(spills, maximum, states[state + top], top);
                    used = kind == CombineHandler ? EventWasm.CombineLists(code, used, left, right, eventLocals) :
                        EventWasm.RemoveLast(code, used, left, right, eventLocals);
                    used = Local(code, used, 0x21, Spill(spills, maximum, EventHandlerReference, top - 1));
                }
                else if (kind == InvokeDelegate && operand == EventHandlerReference)
                {
                    int callback = Spill(spills, maximum, states[state + top - 2], top - 2);
                    used = GuardNull(code, used, callback);
                    used = EventWasm.Invoke(code, used, callback,
                        Spill(spills, maximum, states[state + top - 1], top - 1),
                        Spill(spills, maximum, states[state + top], top), eventLocals);
                }
                else if (kind == InvokeDelegate)
                {
                    int closure = Spill(spills, maximum, states[state + top], top);
                    used = GuardNull(code, used, closure);
                    used = Local(code, used, 0x20, closure);
                    used = Gc(code, used, 2, Delegate); // struct.get receiver
                    code[used++] = 0;
                    used = Local(code, used, 0x20, closure);
                    used = Gc(code, used, 2, Delegate); // struct.get callable reference
                    code[used++] = 1;
                    used = CastThunk(code, used, WasmBinary.ScalarThunkType);
                    code[used++] = 0x14; // call_ref
                    code[used++] = WasmBinary.ScalarThunkType;
                    used = Local(code, used, 0x21, spills + top);
                }
                else if (kind == Store)
                {
                    byte source = states[state + top];
                    used = LoadOperand(code, used, Spill(spills, maximum, source, top), source, frame[operand]);
                    used = NormalizeSlot(code, used, frame[operand]);
                    used = Local(code, used, 0x21, operand);
                }
                else if (kind == CastClass || kind == TestClass)
                {
                    byte type = (byte)operand;
                    used = ReferenceCastWasm.Emit(code, used, Spill(spills, maximum, states[state + top], top),
                        Spill(spills, maximum, type, top), type, kind == CastClass);
                }
                else if (kind == Unary || kind == Narrow || kind == ConvertInteger || kind == ConvertFloat)
                {
                    byte source = states[state + top];
                    byte target = kind == Unary ? source : kind == ConvertFloat ? FloatWasm.Target(operand) :
                        kind == ConvertInteger ? records[slot + 3] == ProvenNativeZero ? NativeZero : IntegerConversionType(operand) : I32;
                    used = Local(code, used, 0x20, Spill(spills, maximum, source, top));
                    if (kind == Unary && IsFloat(source))
                        code[used++] = source == F32 ? (byte)0x8c : (byte)0x9a;
                    else if (kind == ConvertFloat || IsFloat(source))
                    {
                        // The .NET 10 oracle narrows floating values through a
                        // signed saturating i32 conversion before masking.
                        used = FloatWasm.Convert(code, used, kind == Narrow ? 0x69 : operand, source, target);
                    }
                    else if ((source == I64) != (target == I64))
                        code[used++] = source == I64 ? (byte)0xa7 : operand == 0x6a ? (byte)0xac : (byte)0xad;
                    if (kind == Narrow)
                    {
                        if (operand == 0x67 || operand == 0x68)
                            code[used++] = operand == 0x67 ? (byte)0xc0 : (byte)0xc1; // i32.extend8_s / extend16_s
                        else
                        {
                            used = Const(code, used, operand == 0xd2 ? 255 : 65535);
                            code[used++] = 0x71; // i32.and
                        }
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
                    if (type == NullPointer || rightType == NullPointer)
                    {
                        // Admitted pointer operands can only denote null.
                        // The validator accepted equality or a null test.
                        used = Const(code, used, operand == 0x46 ? 1 : 0);
                    }
                    else if (operand == 0x6f && IsFloat(width))
                    {
                        // Any exact remainder of two f32 operands also fits in
                        // f32. Reuse the f64 helper and demote at the IL result.
                        used = NumericLocal(code, used, left, type, F64);
                        used = NumericLocal(code, used, right, rightType, F64);
                        used = FloatRemainderWasm.Call(code, used, frameShape[7], remainderFixup);
                        if (width == F32) code[used++] = 0xb6;
                    }
                    else
                    {
                        if (IsIntegerDivision(operand, type))
                        {
                            used = NumericLocal(code, used, right, rightType, width);
                            code[used++] = type == I64 ? (byte)0x50 : (byte)0x45; // eqz
                            used = ExceptionTagsWasm.ThrowIf(code, used, ExceptionTagsWasm.DivideTag);
                        }
                        if (IsSignedIntegerDivision(operand, type))
                            used = ExceptionTagsWasm.ThrowIf(code, IsMinAndMinusOne(code, used, type, left, right));
                        used = NumericLocal(code, used, left, type, width);
                        used = NumericLocal(code, used, right, rightType, width);
                        if (type == I64 && IsShift(operand)) code[used++] = 0xad;
                        if (IsFloat(width)) used = FloatWasm.Binary(code, used, operand, width);
                        else if (type == NullValue || IsReference(type))
                        {
                            code[used++] = 0xd3; // ref.eq
                            if (operand == 0x4b) code[used++] = 0x45; // cgt.un against null is not-equal
                        }
                        else code[used++] = NumericOpcode(operand, type);
                    }
                    used = Local(code, used, 0x21, Spill(spills, maximum, resultType, top - 1));
                }
                else if (kind == CheckedConversion)
                {
                    byte source = states[state + top];
                    byte target = CheckedConversionWasm.Result(operand);
                    used = CheckedConversionWasm.Emit(code, used, operand, source,
                        Spill(spills, maximum, source, top), Spill(spills, maximum, target, top));
                }
                else if (kind == CheckedInteger)
                {
                    byte type = states[state + top];
                    int left = Spill(spills, maximum, type, top - 1);
                    int right = Spill(spills, maximum, type, top);
                    used = CheckedIntegerWasm.Emit(code, used, operand, type, left, right);
                    used = Local(code, used, 0x21, left);
                }
                else if (kind == Call || kind == NewObject)
                {
                    int arity = arities[operand], shape = offsets[operand];
                    int receiver = kind == NewObject ? 1 : 0;
                    int first = depth - arity + receiver;
                    int temp = 0;
                    if (kind == NewObject)
                    {
                        byte type = StackType(signatures[shape + 1]);
                        temp = temporaries + type - FirstClass;
                        used = Gc(code, used, 1, type); // struct.new_default
                        used = Local(code, used, 0x22, temp);
                    }
                    if (kind == Call && records[slot + 3] != 0)
                    {
                        // The receiver and arguments have already been evaluated.
                        // Keep callvirt's managed null check before calling the known body.
                        int target = Spill(spills, maximum, states[state + first], first);
                        used = GuardNull(code, used, target);
                    }
                    for (int i = receiver; i < arity; i++)
                    {
                        int argument = first + i - receiver;
                        byte source = states[state + argument];
                        used = LoadOperand(code, used, Spill(spills, maximum, source, argument), source, signatures[shape + 1 + i]);
                    }
                    code[used++] = 0x10;
                    used = WasmBinary.Unsigned(code, used, hostImports + operand);
                    int returned = signatures[shape];
                    if (kind == NewObject)
                    {
                        returned = signatures[shape + 1];
                        used = Local(code, used, 0x20, temp);
                    }
                    if (returned != Void)
                        used = Local(code, used, 0x21, Spill(spills, maximum, StackType(returned), first));
                }
                else if (kind == ExchangeHandlerField)
                {
                    byte owner = FieldAccess.Owner(records[slot + 3]);
                    int receiver = Spill(spills, maximum, states[state + top], top);
                    int oldValue = Spill(spills, maximum, EventHandlerReference, top);
                    // The fused ldflda permits only side-effect-free loads after this check.
                    used = GuardNull(code, used, receiver);
                    used = Local(code, used, 0x20, receiver);
                    used = Gc(code, used, 2, owner); // struct.get
                    used = WasmBinary.Unsigned(code, used, operand);
                    used = Local(code, used, 0x22, oldValue);
                    used = Local(code, used, 0x20, records[slot + 2 * BranchIl.Width + 1]);
                    code[used++] = 0xd3; // identity comparison
                    code[used++] = 0x04;
                    code[used++] = 0x40;
                    // This module's GC objects stay on one Wasm agent. The field
                    // read and conditional write execute with no call between them.
                    used = Local(code, used, 0x20, receiver);
                    used = Local(code, used, 0x20, records[slot + BranchIl.Width + 1]);
                    used = Gc(code, used, 5, owner);
                    used = WasmBinary.Unsigned(code, used, operand);
                    code[used++] = 0x0b;
                }
                else if (kind == GetField || kind == SetField)
                {
                    int descriptor = records[slot + 3];
                    byte type = FieldAccess.Owner(descriptor);
                    int storage = FieldAccess.Storage(descriptor);
                    int address = kind == GetField ? top : top - 1;
                    int reference = Spill(spills, maximum, states[state + address], address);
                    if (IsValueType(objects, type))
                        used = ValueFieldsWasm.Materialize(code, used, type, reference);
                    else
                    {
                        used = GuardNull(code, used, reference);
                        used = Local(code, used, 0x20, reference);
                    }
                    if (kind == SetField)
                    {
                        byte source = states[state + top];
                        used = LoadOperand(code, used, Spill(spills, maximum, source, top), source, storage);
                    }
                    if (IsExplicitValue(objects, type))
                        used = ExplicitValueWasm.Load(code, used, objects, type, operand, storage, Spill(spills, maximum, I64, top));
                    else
                    {
                        // Packed cells truncate on assignment. The load selects the CLI extension.
                        int instruction = kind == GetField ? FieldAccess.LoadInstruction(storage) : 5;
                        used = Gc(code, used, instruction, type);
                        used = WasmBinary.Unsigned(code, used, operand);
                    }
                    if (kind == GetField) used = Local(code, used, 0x21, Spill(spills, maximum, StackType(storage), top));
                }
                else if (kind == ClearValue)
                    used = ValueFieldsWasm.Clear(code, used, frame[operand], operand);
                else if (kind == UpdateValue)
                {
                    int descriptor = records[slot + 3];
                    int type = FieldAccess.Owner(descriptor);
                    int local = records[slot + BranchIl.Width + 1] - 1;
                    byte source = states[state + top];
                    int replacement = Spill(spills, maximum, source, top);
                    if (IsExplicitValue(objects, type))
                        used = ExplicitValueWasm.Update(code, used, objects, type, local, operand, FieldAccess.Storage(descriptor), replacement, source);
                    else
                        used = ValueFieldsWasm.Update(code, used, objects, type, local, temporaries + type - FirstClass,
                            operand, replacement, source);
                }
                else if (kind == NewArray || kind == InitializedArray)
                {
                    int length = Spill(spills, maximum, states[state + top], top);
                    if (kind == InitializedArray)
                        used = Const(code, used, records[slot + 3]);
                    else
                    {
                        // Wasm allocation interprets its length as unsigned. The CLI
                        // raises overflow for a negative length before allocation.
                        used = Local(code, used, 0x20, length);
                        used = Const(code, used, 0);
                        code[used++] = 0x48; // i32.lt_s
                        used = ExceptionTagsWasm.ThrowIf(code, used);
                    }
                    used = Local(code, used, 0x20, length);
                    used = Gc(code, used, kind == NewArray ? 7 : 9, operand); // default or passive data
                    if (kind == InitializedArray) code[used++] = 0; // shared data segment index
                    used = Local(code, used, 0x21, Spill(spills, maximum, (byte)operand, top));
                }
                else if (kind == ArrayLength)
                {
                    int reference = Spill(spills, maximum, states[state + top], top);
                    used = GuardNull(code, used, reference);
                    used = Local(code, used, 0x20, reference);
                    code[used++] = 0xfb;
                    code[used++] = 0x0f;
                    used = Local(code, used, 0x21, Spill(spills, maximum, Native, top));
                }
                else if (kind == ArrayGet)
                {
                    byte array = operand == 0x9a ? states[state + top - 1] : ArrayLoadType(operand);
                    int reference = Spill(spills, maximum, states[state + top - 1], top - 1);
                    int index = Spill(spills, maximum, states[state + top], top);
                    used = GuardNull(code, used, reference);
                    used = GuardArrayIndex(code, used, reference, index, false);
                    used = Local(code, used, 0x20, reference);
                    used = Local(code, used, 0x20, index);
                    // The opcode selects the extension of packed cells. Full-
                    // width integer and floating arrays use plain array.get.
                    int instruction = array != Bytes && array != Chars ? 0x0b :
                        operand == 0x90 || operand == 0x92 ? 0x0c : 0x0d;
                    used = Gc(code, used, instruction, array);
                    byte value = operand == 0x9a ? (byte)ArrayElement(objects, array) : ArrayValueType(array);
                    used = Local(code, used, 0x21, Spill(spills, maximum, value, top - 1));
                }
                else if (kind == ArraySet)
                {
                    byte array = operand == 0xa2 ? states[state + top - 2] : ArrayStoreType(operand);
                    int reference = Spill(spills, maximum, states[state + top - 2], top - 2);
                    int index = Spill(spills, maximum, states[state + top - 1], top - 1);
                    used = GuardNull(code, used, reference);
                    used = GuardArrayIndex(code, used, reference, index, false);
                    used = Local(code, used, 0x20, reference);
                    used = Local(code, used, 0x20, index);
                    used = Local(code, used, 0x20, Spill(spills, maximum, states[state + top], top));
                    used = Gc(code, used, 0x0e, array);
                }

                if (kind == Return)
                {
                    if (result != Void)
                    {
                        byte source = states[state + top];
                        used = LoadOperand(code, used, Spill(spills, maximum, source, top), source, result);
                        used = NormalizeSlot(code, used, result);
                    }
                    code[used++] = 0x0f;
                }
                else if (kind == ThrowNull)
                    used = ExceptionTagsWasm.Throw(code, used, ExceptionTagsWasm.NullTag);
                else if (kind == Rethrow)
                    used = ExceptionDispatchWasm.ThrowReference(code, used, pcLocal + 2 + operand);
                else
                {
                    if (hasNull)
                        used = NullSpillWasm.Transfer(code, used, records, pc, depths, states, outgoingNulls, maximum, spills);
                    if (kind == Conditional)
                    {
                        int comparison = records[slot + 3];
                        used = Const(code, used, operand);
                        used = Const(code, used, next);
                        byte type = states[state + top];
                        bool compares = IsComparison(comparison);
                        byte leftType = compares ? states[state + top - 1] : type;
                        if (type == NullPointer || leftType == NullPointer)
                            used = Const(code, used, comparison == 0x45 || comparison == 0x46 ? 1 : 0);
                        else
                        {
                            byte width = compares && IsFloat(type) ? FloatWidth(leftType, type) : type;
                            if (compares)
                                used = NumericLocal(code, used, Spill(spills, maximum, leftType, top - 1), leftType, width);
                            used = NumericLocal(code, used, Spill(spills, maximum, type, top), type, width);
                            if (IsFloat(width)) used = FloatWasm.Binary(code, used, comparison, width);
                            else if (type == NullValue || IsReference(type))
                            {
                                code[used++] = compares ? (byte)0xd3 : (byte)0xd1; // ref.eq or ref.is_null
                                if (comparison == 0 || comparison == 0x47) code[used++] = 0x45;
                            }
                            else if (compares) code[used++] = NumericOpcode(comparison, type);
                            else if (type == I64)
                            {
                                code[used++] = 0x50; // i64.eqz produces the i32 select condition
                                if (comparison == 0) code[used++] = 0x45;
                            }
                            else if (comparison != 0) code[used++] = 0x45;
                        }
                        code[used++] = 0x1b;
                    }
                    else if (kind == Switch)
                        used = SwitchDispatch(code, used, records, operand, records[slot + 3], next, spills + top);
                    else if (kind == EndFinally)
                        used = FinallyWasm.Finish(code, used, pcLocal + 2 + operand, continuationBase + operand);
                    else if (kind == Leave && continuations != 0)
                        used = FinallyWasm.Leave(code, used, pc, operand, pcLocal, continuationBase, catchRegion);
                    else used = Const(code, used, kind == Branch || kind == Leave ? operand : next);
                    used = Local(code, used, 0x21, pcLocal);
                    code[used++] = 0x0c;
                    // Catch dispatch adds try_table and one block per tag around the instruction if.
                    code[used++] = catchRegion.Length == 0 ? (byte)1 : (byte)(2 + ExceptionTagsWasm.Count);
                }
                code[used++] = 0x0b;
            }
            code[used++] = 0;
            if (catchRegion.Length != 0) used = ExceptionDispatchWasm.End(code, used, pcLocal, catchRegion, exceptionLocals > 1);
            code[used++] = 0x0b;
            code[used++] = 0;
            code[used++] = 0x0b;
            return used;
        }

        private static int GuardNull(byte[] code, int used, int reference)
        {
            used = Local(code, used, 0x20, reference);
            code[used++] = 0xd1; // ref.is_null
            return ExceptionTagsWasm.ThrowIf(code, used, ExceptionTagsWasm.NullTag);
        }

        private static int GuardArrayIndex(byte[] code, int used, int reference, int index, bool wrapped)
        {
            // A managed null check precedes this helper, even for negative indices.
            used = Local(code, used, 0x20, index);
            used = Local(code, used, 0x20, reference);
            if (wrapped) used = StringWasm.Unwrap(code, used);
            code[used++] = 0xfb;
            code[used++] = 0x0f; // array.len
            // An unsigned comparison rejects negative indices and indices at or above length.
            code[used++] = 0x4f;
            return ExceptionTagsWasm.ThrowIf(code, used, ExceptionTagsWasm.IndexTag);
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

        internal static byte NumericOpcode(int instruction, byte type) =>
            (byte)(type == I64 ? instruction + (IsComparison(instruction) ? 0x0b : 0x12) : instruction);

        internal static int IsMinAndMinusOne(byte[] code, int used, byte type, int left, int right)
        {
            used = Local(code, used, 0x20, left);
            used = type == I64 ? WideConst(code, used, 0, int.MinValue) : Const(code, used, int.MinValue);
            code[used++] = NumericOpcode(0x46, type);
            used = Local(code, used, 0x20, right);
            used = type == I64 ? WideConst(code, used, -1, -1) : Const(code, used, -1);
            code[used++] = NumericOpcode(0x46, type);
            code[used++] = 0x71; // i32.and
            return used;
        }

        internal static int Spill(int first, int maximum, byte type, int depth) =>
            type == NullValue ? first - 1 : first + ((type == NativeZero ? Native : type) - 1) * maximum + depth;

        internal static int LoadOperand(byte[] code, int used, int local, byte source, int target)
        {
            if (source != NativeZero || target != NullPointer) return Local(code, used, 0x20, local);
            code[used++] = 0xd0; // ref.null none
            code[used++] = NullSpillWasm.NullRef;
            return used;
        }

        private static int NormalizeSlot(byte[] code, int used, int type)
        {
            if (type == Signed8 || type == Signed16)
                code[used++] = type == Signed8 ? (byte)0xc0 : (byte)0xc1; // i32.extend8_s / extend16_s
            else if (IsNarrow(type))
            {
                used = Const(code, used, type == Unsigned8 ? 255 : 65535);
                code[used++] = 0x71; // i32.and
            }
            return used;
        }

        private static int CastThunk(byte[] code, int used, byte type)
        {
            code[used++] = 0xfb;
            code[used++] = 22; // ref.cast from the shared table's funcref
            code[used++] = type;
            return used;
        }

        internal static int Gc(byte[] code, int used, int instruction, int type)
        {
            code[used++] = 0xfb;
            code[used++] = (byte)instruction;
            return WasmBinary.Unsigned(code, used, WasmBinary.HeapType((byte)type));
        }

        internal static int Const(byte[] code, int used, int value)
        {
            code[used++] = 0x41;
            return WasmBinary.Signed(code, used, value);
        }

        internal static int WideConst(byte[] code, int used, int low, int high)
        {
            code[used++] = 0x42;
            return WasmBinary.Signed64(code, used, low, high);
        }

        internal static int Local(byte[] code, int used, byte instruction, int local)
        {
            code[used++] = instruction;
            return WasmBinary.Unsigned(code, used, local);
        }
    }
}
