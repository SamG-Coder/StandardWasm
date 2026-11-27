using static PortableCompiler.BranchIl;
using static PeLibrary.ObjectLayouts;
using static PortableCompiler.SignatureTypes;

namespace PortableCompiler
{
    internal static class BranchWasm
    {
        private const int LastScalarBank = PeLibrary.ConsoleStorage.Writer;
        private const int LocationSpill = 1, SpanSpill = 2;
        internal static int Emit(int[] records, int[] depths, int[] states, int[] frameShape, int[] frame,
            int[] signatures, int[] offsets, int[] arities, byte[] code, int[] remainderFixup, int[] catchRegion,
            byte[] outgoingNulls, int[] objects, int[] fieldInitializers, RuntimeReflectionPlan reflection, int[] inlineLocations, int[] nativePlans, int[] filters)
        {
            int arguments = frameShape[0], locals = frameShape[1], maximum = frameShape[2];
            int result = frameShape[3];
            int classes = frameShape[4];
            bool stringScratch = frameShape[5] != 0;
            bool eventScratch = frameShape[6] != 0;
            int exceptionLocals = frameShape[8], continuations = frameShape[9];
            int hostImports = frameShape[10];
            bool valueConstructor = frameShape[11] != 0;
            // Stage 129 (decision 0057): a precise-type member calls its type
            // initializer on entry. An initializer body runs behind its guard.
            int prologue = frameShape[12] - 1, guard = frameShape[13] - 1, epoch = frameShape[14] - 1;
            int activeInitializers = frameShape[15] - 1;
            // Stage 137 (decision 0065): a frame that uses localloc owns a slice of the shadow stack.
            int stackPointer = frameShape[16] - 1, stackBase = frameShape[17] - 1, stackLimit = frameShape[18] - 1;
            int resourceFunction = frameShape[19] - 1;
            int staticRegion = frameShape[21] - 1, staticPages = frameShape[22];
            bool hasNull = outgoingNulls.Length != 0;
            int spillFlags = 0;
            // Only verified stack types need banks. Unused view types must not
            // reduce the local budget of methods with many ordinary classes.
            for (int i = 0; i < states.Length && spillFlags != (LocationSpill | SpanSpill); i++)
            {
                if (IsManagedLocation(states[i])) spillFlags |= LocationSpill;
                if (IsSpan(states[i])) spillFlags |= SpanSpill;
            }
            int classBase = LastScalarBank + ExtraSpillBanks(spillFlags);
            int spillKinds = classBase + classes;
            // V8 counts parameters together with function locals. Reject
            // before emitting a body that Node and Edge cannot instantiate.
            const int MaxLocals = 50000;
            if (arguments > MaxLocals || locals > MaxLocals || exceptionLocals > MaxLocals ||
                continuations > MaxLocals) return -2;
            int spills = arguments + locals + (hasNull ? 1 : 0);
            int fixedLocals = spills + classes + (stringScratch ? 5 : 0) +
                (eventScratch ? 6 : 0) + (resourceFunction >= 0 ? 7 : 0) + 1 + 2 * exceptionLocals + continuations +
                (stackPointer >= 0 ? 1 : 0) + (staticRegion >= 0 ? 1 : 0);
            if (fixedLocals > MaxLocals || maximum > (MaxLocals - fixedLocals) / spillKinds) return -2;
            int temporaries = spills + spillKinds * maximum;
            int scratch = temporaries + classes;
            int eventLocals = scratch + (stringScratch ? 5 : 0);
            int resourceLocals = eventLocals + (eventScratch ? 6 : 0);
            int pcLocal = resourceLocals + (resourceFunction >= 0 ? 7 : 0);
            int continuationBase = pcLocal + 1 + exceptionLocals;
            // Thrown managed objects (decision 0054) mirror the exnref locals: a scratch
            // eqref followed by one saved object per clause.
            int objectBase = continuationBase + continuations;
            int used = WasmBinary.Unsigned(code, 0, locals + classes + (hasNull ? 1 : 0) + (maximum == 0 ? 1 : 1 + spillKinds) +
                (stringScratch ? 2 : 0) + (eventScratch ? 2 : 0) + (resourceFunction >= 0 ? 3 : 0) + (exceptionLocals != 0 ? 1 : 0) +
                (continuations != 0 ? 1 : 0) + (exceptionLocals != 0 ? 1 : 0) + (stackPointer >= 0 ? 1 : 0) +
                (staticRegion >= 0 ? 1 : 0));
            // The saved shadow-stack pointer follows every other local.
            int stackFrame = objectBase + exceptionLocals;
            int staticAddress = stackFrame + (stackPointer >= 0 ? 1 : 0);
            // Keep declared locals in CLI order, then allocate one bank per spill type.
            for (int i = arguments; i < arguments + locals; i++)
            {
                code[used++] = 1;
                int type = inlineLocations[i] == 0 ? frame[i] : inlineLocations[i];
                used = WasmBinary.ValueType(code, used, Representation(objects, type));
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
                    int type = kind == NativeZero ? NativeVector : kind;
                    if (kind > classBase)
                        type = FirstClass + kind - classBase - 1;
                    else if (kind > LastScalarBank)
                        type = kind == LastScalarBank + 1 && (spillFlags & LocationSpill) != 0 ? ObjectLocation : ObjectSpan;
                    used = WasmBinary.ValueType(code, used, Representation(objects, type));
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
            if (resourceFunction >= 0)
            {
                code[used++] = 1;
                used = WasmBinary.ValueType(code, used, Chars);
                code[used++] = 5; // owner, length, address, copy index, status
                code[used++] = 0x7f;
                code[used++] = 1;
                code[used++] = 0x7e; // packed result
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
            if (exceptionLocals != 0)
            {
                used = WasmBinary.Unsigned(code, used, exceptionLocals);
                code[used++] = ExceptionDispatchWasm.ObjectReference;
            }
            if (stackPointer >= 0)
            {
                code[used++] = 1;
                code[used++] = 0x7f;
            }
            if (staticRegion >= 0)
            {
                code[used++] = 1;
                code[used++] = 0x7f;
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
            for (int local = arguments; local < inlineLocations.Length; local++)
                if (inlineLocations[local] != 0)
                    used = InlineObjectStorageWasm.Allocate(code, used, inlineLocations[local], local, FieldCount(objects, frame[local]));
            if (prologue >= 0)
            {
                code[used++] = 0x10; // call the initializer after the caller evaluated the arguments
                used = WasmBinary.Unsigned(code, used, hostImports + prologue);
            }
            if (guard >= 0) used = TypeInitializerWasm.Begin(code, used, guard, epoch, activeInitializers, pcLocal);
            if (stackPointer >= 0)
            {
                used = StackAllocationWasm.Begin(code, used, stackPointer, stackFrame);
                // An exception leaving this frame releases its allocations, then continues.
                code[used++] = 0x02; code[used++] = ExceptionDispatchWasm.ExceptionReference; // block (result exnref)
                code[used++] = 0x1f; code[used++] = 0x40; code[used++] = 1;                  // try_table
                code[used++] = 0x03; code[used++] = 0;                                         // catch_all_ref 0
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
                bool fieldStore = kind == SetField || kind == UpdateConstructorField;
                used = Local(code, used, 0x20, pcLocal);
                used = Const(code, used, pc);
                code[used++] = 0x46;
                code[used++] = 0x04;
                code[used++] = 0x40;

                if (kind == Load || kind == Constant || kind == WideConstant || (kind == Dup && states[state + top] != NullValue) ||
                    kind == SingleConstant || kind == DoubleConstant)
                {
                    int pushed = I32;
                    if (kind == Load)
                    {
                        pushed = StackType(frame[operand]);
                        used = inlineLocations[operand] == 0 ? Local(code, used, 0x20, operand) :
                            InlineObjectStorageWasm.Snapshot(code, used, pushed, inlineLocations[operand], operand, FieldCount(objects, pushed));
                    }
                    else if (kind == Dup)
                    {
                        pushed = states[state + top];
                        used = Local(code, used, 0x20, Spill(spills, maximum, spillFlags, pushed, top));
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
                    used = Local(code, used, 0x21, Spill(spills, maximum, spillFlags, pushed, depth));
                }
                else if (kind == LoadTypeToken)
                {
                    code[used++] = 0x23; // global.get canonical runtime type descriptor
                    used = WasmBinary.Unsigned(code, used, operand);
                    used = Local(code, used, 0x21, Spill(spills, maximum, spillFlags, PeLibrary.RuntimeTypeMetadata.Handle, depth));
                }
                else if (kind == GetObjectType)
                {
                    int type = states[state + top];
                    reflection.ObjectTypes.Include(type);
                    int receiver = Spill(spills, maximum, spillFlags, type, top);
                    used = GuardNull(code, used, receiver);
                    used = Local(code, used, 0x20, receiver);
                    code[used++] = 0x10; // call the type lookup after all layouts are linked
                    used = WasmIndexFixups.Write(code, used, frameShape[7], reflection.ObjectTypes.Fixups);
                    used = Local(code, used, 0x21, Spill(spills, maximum, spillFlags, PeLibrary.RuntimeTypeMetadata.Type, top));
                }
                else if (kind == EqualRuntimeTypes)
                {
                    used = Local(code, used, 0x20, Spill(spills, maximum, spillFlags, states[state + top - 1], top - 1));
                    used = Local(code, used, 0x20, Spill(spills, maximum, spillFlags, states[state + top], top));
                    code[used++] = 0xd3; // ref.eq on canonical Type descriptors
                    used = Local(code, used, 0x21, Spill(spills, maximum, spillFlags, I32, top - 1));
                }
                // TypeFromHandle changes the CLI category while retaining the reference in the same spill.
                else if (kind == ReadReflection)
                {
                    int source = states[state + top];
                    int target = operand == PeLibrary.ReflectionMembers.ModuleName ? String : ObjectReference;
                    int receiver = Spill(spills, maximum, spillFlags, source, top);
                    used = GuardNull(code, used, receiver);
                    used = RuntimeReflectionWasm.Read(code, used, frameShape[7], operand, receiver,
                        Spill(spills, maximum, spillFlags, target, top), pcLocal, reflection);
                }
                else if (kind == ResourceLookup)
                {
                    used = RuntimeReflectionWasm.ModuleSlot(code, used, frameShape[7], 0, reflection);
                    used = Const(code, used, 1);
                    code[used++] = 0x6b; // descriptor slot to zero-based bundle owner
                    used = Local(code, used, 0x21, resourceLocals + 1);
                    used = ResourceWasm.LookupNative(code, used, resourceLocals + 1, 1,
                        resourceLocals, resourceLocals + 2, resourceFunction,
                        records[slot + 3], resourceLocals + 5, resourceLocals + 6);
                    used = Local(code, used, 0x21, operand);
                    used = ResourceWasm.ThrowForFailure(code, used, resourceLocals + 5);
                }
                else if (kind == LoadString)
                {
                    int global = records[slot + BranchIl.Width + 1];
                    used = StringWasm.LoadLiteral(code, used, global, operand, records[slot + 3]);
                    used = Local(code, used, 0x21, Spill(spills, maximum, spillFlags, String, depth));
                }
                else if (kind == NewRootObject)
                {
                    // A fresh, identity-bearing plain object. It has no fields and no
                    // virtual members; Object member calls on it stay diagnosed.
                    code[used++] = 0xfb;
                    code[used++] = 1; // struct.new_default
                    used = WasmBinary.Unsigned(code, used, WasmBinary.RootObjectHeap);
                    used = Local(code, used, 0x21, Spill(spills, maximum, spillFlags, ObjectReference, depth));
                }
                else if (kind == LoadEventArgsEmpty || kind == NewEventArgs)
                {
                    if (kind == LoadEventArgsEmpty)
                    {
                        code[used++] = 0x23; // global.get EventArgs.Empty
                        used = WasmBinary.Unsigned(code, used, operand);
                    }
                    else used = Gc(code, used, 1, EventArgsReference); // struct.new_default
                    used = Local(code, used, 0x21, Spill(spills, maximum, spillFlags, EventArgsReference, depth));
                }
                else if (kind == StringEquals)
                {
                    int left = Spill(spills, maximum, spillFlags, states[state + top - 1], top - 1);
                    int right = Spill(spills, maximum, spillFlags, states[state + top], top);
                    used = StringWasm.OrdinalEquals(code, used, left, right, spills + top - 1, scratch);
                }
                else if (kind == JoinStrings || kind == StringFromChars)
                {
                    bool join = kind == JoinStrings;
                    int at = top - (join ? 1 : 0);
                    used = StringWasm.Copy(code, used, Spill(spills, maximum, spillFlags, states[state + at], at),
                        Spill(spills, maximum, spillFlags, states[state + top], top), scratch, join, operand);
                    used = Local(code, used, 0x21, Spill(spills, maximum, spillFlags, String, at));
                }
                else if (kind == StringLength || kind == StringCharacter)
                {
                    int at = top - (kind == StringCharacter ? 1 : 0);
                    int reference = Spill(spills, maximum, spillFlags, states[state + at], at);
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
                    used = Local(code, used, 0x21, Spill(spills, maximum, spillFlags, PeLibrary.ConsoleStorage.Writer, depth));
                }
                else if (kind == ConsoleWrite)
                {
                    bool text = operand != PeLibrary.ConsoleMetadata.WriteEmptyLine;
                    int writer = text ? top - 1 : top;
                    // The String scratch bank supplies a Char[] and two i32 locals.
                    used = ConsoleWasm.Write(code, used, operand,
                        Spill(spills, maximum, spillFlags, PeLibrary.ConsoleStorage.Writer, writer),
                        text ? Spill(spills, maximum, spillFlags, states[state + top], top) : 0,
                        scratch, scratch + 3, scratch + 4);
                }
                else if (kind == LoadFunction)
                {
                    used = Const(code, used, operand);
                    used = Local(code, used, 0x21, Spill(spills, maximum, spillFlags, MethodPointer, depth));
                }
                else if (kind == NewDelegate)
                {
                    int receiver = records[slot + 3];
                    int closureType = operand;
                    int target = Spill(spills, maximum, spillFlags, receiver, top - 1);
                    // CLR rejects binding an instance delegate to a null target.
                    used = GuardNull(code, used, target, ExceptionTagsWasm.ArgumentTag);
                    used = Local(code, used, 0x20, target);
                    code[used++] = 0xd4;
                    int pointer = Spill(spills, maximum, spillFlags, MethodPointer, top);
                    int destination = Spill(spills, maximum, spillFlags, closureType, top - 1);
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
                    int left = Spill(spills, maximum, spillFlags, states[state + top - 1], top - 1);
                    int right = Spill(spills, maximum, spillFlags, states[state + top], top);
                    used = kind == CombineHandler ? EventWasm.CombineLists(code, used, left, right, eventLocals) :
                        EventWasm.RemoveLast(code, used, left, right, eventLocals);
                    used = Local(code, used, 0x21, Spill(spills, maximum, spillFlags, EventHandlerReference, top - 1));
                }
                else if (kind == InvokeDelegate && operand == EventHandlerReference)
                {
                    int callback = Spill(spills, maximum, spillFlags, states[state + top - 2], top - 2);
                    used = GuardNull(code, used, callback);
                    used = EventWasm.Invoke(code, used, callback,
                        Spill(spills, maximum, spillFlags, states[state + top - 1], top - 1),
                        Spill(spills, maximum, spillFlags, states[state + top], top), eventLocals);
                }
                else if (kind == InvokeDelegate)
                {
                    int closure = Spill(spills, maximum, spillFlags, states[state + top], top);
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
                    int source = states[state + top];
                    int spilled = Spill(spills, maximum, spillFlags, source, top);
                    int type = frame[operand], backing = inlineLocations[operand];
                    if (backing != 0)
                        used = InlineObjectStorageWasm.Store(code, used, type, backing, operand, spilled, FieldCount(objects, type));
                    else
                    {
                        used = LoadOperand(code, used, spilled, source, type);
                        used = NormalizeSlot(code, used, type);
                        used = Local(code, used, 0x21, operand);
                    }
                }
                else if (kind == BoxValue || kind == UnboxValue || kind == TestBox)
                {
                    int local = Spill(spills, maximum, spillFlags, states[state + top], top);
                    int type = records[slot + 3];
                    if (kind == BoxValue) used = PrimitiveBoxing.Box(code, used, local, operand, type);
                    else if (kind == UnboxValue) used = PrimitiveBoxing.Unbox(code, used, local, operand, type);
                    else used = PrimitiveBoxing.Test(code, used, local, operand, type);
                    int resultType = kind == UnboxValue ? StackType(PrimitiveBoxing.Storage(operand)) : ObjectReference;
                    used = Local(code, used, 0x21, Spill(spills, maximum, spillFlags, resultType, top));
                }
                else if (kind == CastClass || kind == TestClass)
                {
                    int type = operand;
                    used = ReferenceCastWasm.Emit(code, used, Spill(spills, maximum, spillFlags, states[state + top], top),
                        Spill(spills, maximum, spillFlags, type, top), type, kind == CastClass);
                }
                else if (kind == Unary || kind == Narrow || kind == ConvertInteger || kind == ConvertFloat)
                {
                    int source = states[state + top];
                    int target = kind == Unary ? source : kind == ConvertFloat ? FloatWasm.Target(operand) :
                        kind == ConvertInteger ? records[slot + 3] == ProvenNativeZero ? NativeZero : IntegerConversionType(operand) : I32;
                    used = Local(code, used, 0x20, Spill(spills, maximum, spillFlags, source, top));
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
                    used = Local(code, used, 0x21, Spill(spills, maximum, spillFlags, target, top));
                }
                else if (kind == Binary)
                {
                    int type = states[state + top - 1];
                    int rightType = states[state + top];
                    int width = IsFloat(type) ? FloatWidth(type, rightType) : type;
                    int resultType = IsComparison(operand) ? I32 : width;
                    int left = Spill(spills, maximum, spillFlags, type, top - 1);
                    int right = Spill(spills, maximum, spillFlags, states[state + top], top);
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
                    used = Local(code, used, 0x21, Spill(spills, maximum, spillFlags, resultType, top - 1));
                }
                else if (kind == CheckedConversion)
                {
                    int source = states[state + top];
                    int target = CheckedConversionWasm.Result(operand);
                    used = CheckedConversionWasm.Emit(code, used, operand, source,
                        Spill(spills, maximum, spillFlags, source, top), Spill(spills, maximum, spillFlags, target, top));
                }
                else if (kind == CheckedInteger)
                {
                    int type = states[state + top], leftType = states[state + top - 1];
                    bool native = IsNativeInteger(type) || IsNativeInteger(leftType);
                    int left = Spill(spills, maximum, spillFlags, leftType, top - 1);
                    int right = Spill(spills, maximum, spillFlags, type, top);
                    used = CheckedIntegerWasm.Emit(code, used, operand, native ? I32 : type, left, right);
                    used = Local(code, used, 0x21, native ? Spill(spills, maximum, spillFlags, Native, top - 1) : left);
                }
                else if (kind == Call || kind == NewObject)
                {
                    int arity = arities[operand], shape = offsets[operand];
                    int receiver = kind == NewObject ? 1 : 0;
                    int first = depth - arity + receiver;
                    int temp = 0;
                    bool valueObject = kind == NewObject && IsValueType(objects, signatures[shape + 1]);
                    if (valueObject)
                    {
                        // CLI newobj on a value type constructs a fresh zeroed
                        // instance. Null is the default aggregate, and the
                        // constructor returns the constructed value: [args] -> [S].
                        code[used++] = 0xd0; // ref.null
                        used = WasmBinary.Signed(code, used, WasmBinary.HeapType(signatures[shape + 1]));
                    }
                    else if (kind == NewObject)
                    {
                        int type = StackType(signatures[shape + 1]);
                        temp = temporaries + type - FirstClass;
                        used = Gc(code, used, 1, type); // struct.new_default
                        used = Local(code, used, 0x22, temp);
                    }
                    if (kind == Call && (records[slot + 3] & 1) != 0)
                    {
                        // The receiver and arguments have already been evaluated.
                        // Keep callvirt's managed null check before calling the known body.
                        int target = Spill(spills, maximum, spillFlags, states[state + first], first);
                        used = GuardNull(code, used, target);
                    }
                    for (int i = receiver; i < arity; i++)
                    {
                        int argument = first + i - receiver;
                        int source = states[state + argument];
                        used = LoadOperand(code, used, Spill(spills, maximum, spillFlags, source, argument), source, signatures[shape + 1 + i]);
                    }
                    int returned = signatures[shape];
                    // Preserve the current frame whenever it can handle an exception.
                    // With the same result type, the callee already normalizes its return value.
                    // An initializer must observe its callee's failure, so it never tail-calls.
                    if (kind == Call && catchRegion.Length == 0 && guard < 0 && stackPointer < 0 && first == 0 && returned == result &&
                        next < depths.Length && records[next * Width] == Return)
                    {
                        code[used++] = 0x12; // return_call
                        used = WasmBinary.Unsigned(code, used, hostImports + operand);
                        code[used++] = 0x0b; // end the dispatch block
                        continue;
                    }
                    code[used++] = 0x10;
                    used = WasmBinary.Unsigned(code, used, hostImports + operand);
                    if (kind == NewObject && !valueObject)
                    {
                        returned = signatures[shape + 1];
                        used = Local(code, used, 0x20, temp);
                    }
                    if (returned != Void)
                        used = Local(code, used, 0x21, Spill(spills, maximum, spillFlags, StackType(returned), first));
                }
                else if (kind == ConstructValue)
                {
                    // [S, arguments] -> []. The CLR runs the constructor on the
                    // location's address. Here the callee receives the location's
                    // aggregate as it is at the call and returns the replacement,
                    // which is stored back. Copies taken earlier keep their values.
                    int arity = arities[operand], shape = offsets[operand];
                    int first = depth - arity;
                    int target = records[slot + 3];
                    int type = frame[target], backing = inlineLocations[target];
                    used = backing == 0 ? Local(code, used, 0x20, target) :
                        InlineObjectStorageWasm.Snapshot(code, used, type, backing, target, FieldCount(objects, type));
                    for (int i = 1; i < arity; i++)
                    {
                        int argument = first + i;
                        int source = states[state + argument];
                        used = LoadOperand(code, used, Spill(spills, maximum, spillFlags, source, argument), source, signatures[shape + 1 + i]);
                    }
                    code[used++] = 0x10;
                    used = WasmBinary.Unsigned(code, used, hostImports + operand);
                    int temporary = backing == 0 ? target : temporaries + type - FirstClass;
                    used = Local(code, used, 0x21, temporary);
                    if (backing != 0)
                        used = InlineObjectStorageWasm.Store(code, used, type, backing, target, temporary, FieldCount(objects, type));
                }
                else if (kind == MathCall)
                {
                    byte type = MathWasm.Type(operand);
                    int first = top - MathWasm.Arity(operand) + 1;
                    used = MathWasm.Emit(code, used, operand, Spill(spills, maximum, spillFlags, type, first), Spill(spills, maximum, spillFlags, type, top));
                    used = Local(code, used, 0x21, Spill(spills, maximum, spillFlags, type, first));
                }
                else if (kind == ExchangeHandlerField)
                {
                    int owner = FieldAccess.Owner(records[slot + 3]);
                    int receiver = Spill(spills, maximum, spillFlags, states[state + top], top);
                    int oldValue = Spill(spills, maximum, spillFlags, EventHandlerReference, top);
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
                else if (kind == Monitor)
                {
                    int target = Spill(spills, maximum, spillFlags, states[state + top], top);
                    used = MonitorWasm.Emit(code, used, target, operand, frameShape[23]);
                }
                else if (kind == Win32Error)
                {
                    bool setter = PeLibrary.MarshalErrorMetadata.IsSetter(operand);
                    if (setter) used = Local(code, used, 0x20, Spill(spills, maximum, spillFlags, I32, top));
                    code[used++] = 0x10;
                    used = WasmBinary.Unsigned(code, used, frameShape[20] + operand - 1);
                    if (!setter) used = Local(code, used, 0x21, Spill(spills, maximum, spillFlags, I32, depth));
                }
                else if (kind == LoadObject)
                {
                    int address = Spill(spills, maximum, spillFlags, Native, top);
                    used = LinearMemoryWasm.GuardNull(code, used, address);
                    used = NativeAggregates.Load(code, used, nativePlans, records[slot + 3] - 1, address, 0);
                    used = Local(code, used, 0x21, Spill(spills, maximum, spillFlags, operand, top));
                }
                else if (kind == StoreObject)
                {
                    // CLI order: address and value are evaluated, then the store checks null.
                    int address = Spill(spills, maximum, spillFlags, Native, top - 1);
                    int value = Spill(spills, maximum, spillFlags, operand, top);
                    used = LinearMemoryWasm.GuardNull(code, used, address);
                    if (states[state + top] == NullValue) used = ValueFieldsWasm.Clear(code, used, operand, value);
                    used = ValueFieldsWasm.Materialize(code, used, operand, value);
                    used = Local(code, used, 0x21, value);
                    used = NativeAggregates.Store(code, used, nativePlans, objects, records[slot + 3] - 1, address, 0,
                        value, spills, maximum, spillFlags, top);
                }
                else if (kind == InitializeNative)
                {
                    int address = Spill(spills, maximum, spillFlags, Native, top);
                    used = LinearMemoryWasm.GuardNull(code, used, address);
                    used = NativeAggregates.Clear(code, used, nativePlans, records[slot + 3] - 1, address);
                }
                else if (kind == PinString)
                {
                    // The String scratch bank supplies the Char[] and two i32 locals.
                    int source = Spill(spills, maximum, spillFlags, String, top);
                    if (records[slot + 3] != 0) used = GuardNull(code, used, source);
                    used = Utf16MemoryWasm.Pin(code, used, source, scratch, scratch + 3, scratch + 4,
                        Spill(spills, maximum, spillFlags, Native, top), stackPointer, stackBase, stackLimit, pcLocal);
                }
                else if (kind == LocalAllocate)
                {
                    int size = Spill(spills, maximum, spillFlags, states[state + top], top);
                    used = StackAllocationWasm.Allocate(code, used, stackPointer, stackBase, stackLimit, size,
                        Spill(spills, maximum, spillFlags, Native, top), pcLocal);
                }
                else if (kind == LoadInlineStorage)
                {
                    used = Local(code, used, 0x20, operand);
                    used = Local(code, used, 0x21, Spill(spills, maximum, spillFlags, records[slot + 3], depth));
                }
                else if (kind == NewObjectSpan)
                {
                    int vector = Spill(spills, maximum, spillFlags, operand, top - 1);
                    int count = Spill(spills, maximum, spillFlags, I32, top);
                    int resultSpill = Spill(spills, maximum, spillFlags, ObjectSpan, top - 1);
                    used = SpanWasm.Create(code, used, vector, count, resultSpill);
                }
                else if (kind == StringAsSpan)
                {
                    int source = Spill(spills, maximum, spillFlags, states[state + top], top);
                    used = SpanWasm.FromOwner(code, used, source, true);
                    used = Local(code, used, 0x21, Spill(spills, maximum, spillFlags, CharacterSpan, top));
                }
                else if (kind == CharacterArrayAsSpan)
                {
                    int source = Spill(spills, maximum, spillFlags, states[state + top], top);
                    int target = records[slot + 3] - 1;
                    used = SpanWasm.FromOwner(code, used, source, false);
                    used = Local(code, used, 0x21, target >= 0 ? target : Spill(spills, maximum, spillFlags, WritableCharacterSpan, top));
                }
                // A SpanAsReadOnly operation only changes the logical type.
                // Both descriptors occupy the same Wasm spill bank.
                else if (kind == SpanLength)
                {
                    int type = records[slot + 3];
                    used = SpanWasm.Length(code, used, Spill(spills, maximum, spillFlags, type, top));
                    used = Local(code, used, 0x21, Spill(spills, maximum, spillFlags, I32, top));
                }
                else if (kind == SpanAddress)
                {
                    int view = Spill(spills, maximum, spillFlags, WritableCharacterSpan, top - 1);
                    int index = Spill(spills, maximum, spillFlags, I32, top);
                    used = SpanWasm.Address(code, used, Chars, view, index);
                    used = Gc(code, used, 0, CharacterLocation); // struct.new traced location
                    used = Local(code, used, 0x21, Spill(spills, maximum, spillFlags, CharacterLocation, top - 1));
                }
                else if (kind == SpanRead)
                {
                    int type = records[slot + 3];
                    int view = Spill(spills, maximum, spillFlags, type, top - 1);
                    int index = Spill(spills, maximum, spillFlags, I32, top);
                    used = SpanWasm.Read(code, used, operand, view, index);
                    int resultType = type == CharacterSpan ? I32 : ObjectReference;
                    used = Local(code, used, 0x21, Spill(spills, maximum, spillFlags, resultType, top - 1));
                }
                else if (kind == ObjectElementAddress)
                {
                    int array = Spill(spills, maximum, spillFlags, states[state + top - 1], top - 1);
                    int index = Spill(spills, maximum, spillFlags, states[state + top], top);
                    int resultSpill = Spill(spills, maximum, spillFlags, ObjectLocation, top - 1);
                    used = ManagedLocationWasm.TakeElement(code, used, array, index, resultSpill);
                }
                else if (kind == LoadObjectIndirect)
                {
                    int address = Spill(spills, maximum, spillFlags, ObjectLocation, top);
                    used = ManagedLocationWasm.AccessElement(code, used, address, operand);
                    used = Gc(code, used, 0x0b, operand); // array.get
                    used = Local(code, used, 0x21, Spill(spills, maximum, spillFlags, ObjectReference, top));
                }
                else if (kind == StoreObjectIndirect)
                {
                    int address = Spill(spills, maximum, spillFlags, ObjectLocation, top - 1);
                    used = ManagedLocationWasm.AccessElement(code, used, address, operand);
                    used = Local(code, used, 0x20, Spill(spills, maximum, spillFlags, states[state + top], top));
                    used = Gc(code, used, 0x0e, operand); // array.set
                }
                else if (kind == LoadIndirect)
                {
                    int type = states[state + top];
                    int address = Spill(spills, maximum, spillFlags, type, top);
                    if (type == CharacterLocation)
                    {
                        used = ManagedLocationWasm.AccessElement(code, used, address, Chars);
                        used = Gc(code, used, 0x0d, Chars); // array.get_u
                    }
                    else
                    {
                        used = LinearMemoryWasm.GuardNull(code, used, address);
                        used = Local(code, used, 0x20, address);
                        used = LinearMemoryWasm.Load(code, used, operand);
                    }
                    used = Local(code, used, 0x21, Spill(spills, maximum, spillFlags, StackDepths.IndirectType(operand), top));
                }
                else if (kind == StoreIndirect)
                {
                    // CLI order: the address and the value are both evaluated, then the
                    // store raises NullReferenceException for address 0.
                    int type = states[state + top - 1], value = states[state + top];
                    int address = Spill(spills, maximum, spillFlags, type, top - 1);
                    int source = Spill(spills, maximum, spillFlags, value, top);
                    if (type == CharacterLocation)
                    {
                        used = ManagedLocationWasm.AccessElement(code, used, address, Chars);
                        used = Local(code, used, 0x20, source);
                        used = Gc(code, used, 0x0e, Chars); // array.set truncates to i16
                    }
                    else
                    {
                        used = LinearMemoryWasm.GuardNull(code, used, address);
                        used = Local(code, used, 0x20, address);
                        used = Local(code, used, 0x20, source);
                        if (operand == 0x56 && value == F64) code[used++] = 0xb6; // f32.demote_f64
                        if (operand == 0x57 && value == F32) code[used++] = 0xbb; // f64.promote_f32
                        used = LinearMemoryWasm.Store(code, used, operand);
                    }
                }
                else if (kind == StaticFieldAddress || kind == LoadNativeStatic || kind == StoreNativeStatic)
                {
                    int type = records[slot + 3];
                    int plan = records[slot + Width + 1], offset = records[slot + Width + 2];
                    used = TypeInitializerWasm.Trigger(code, used, fieldInitializers, operand, hostImports);
                    used = NativeStaticWasm.Address(code, used, staticRegion, staticPages, offset, staticAddress);
                    if (kind == StaticFieldAddress)
                    {
                        used = Local(code, used, 0x20, staticAddress);
                        used = Local(code, used, 0x21, Spill(spills, maximum, spillFlags, Native, depth));
                    }
                    else if (kind == LoadNativeStatic)
                    {
                        used = NativeAggregates.Load(code, used, nativePlans, plan, staticAddress, 0);
                        used = Local(code, used, 0x21, Spill(spills, maximum, spillFlags, type, depth));
                    }
                    else
                    {
                        int value = Spill(spills, maximum, spillFlags, type, top);
                        if (states[state + top] == NullValue) used = ValueFieldsWasm.Clear(code, used, type, value);
                        used = ValueFieldsWasm.Materialize(code, used, type, value);
                        used = Local(code, used, 0x21, value);
                        used = NativeAggregates.Store(code, used, nativePlans, objects, plan, staticAddress, 0,
                            value, spills, maximum, spillFlags, top);
                    }
                }
                else if (kind == LoadStaticField)
                {
                    used = TypeInitializerWasm.Trigger(code, used, fieldInitializers, operand, hostImports);
                    code[used++] = 0x23; // global.get
                    used = WasmBinary.Unsigned(code, used, operand);
                    int storage = StackType(records[slot + 3]);
                    used = Local(code, used, 0x21, Spill(spills, maximum, spillFlags, storage, depth));
                }
                else if (kind == StoreStaticField)
                {
                    int storage = records[slot + 3], source = states[state + top];
                    used = LoadOperand(code, used, Spill(spills, maximum, spillFlags, source, top), source, storage);
                    // Globals have full-width Wasm value types. Normalize CLI
                    // byte and short assignments before persisting them.
                    used = NormalizeSlot(code, used, storage);
                    // CLI order: the right-hand side, then the initializer, then the write.
                    used = TypeInitializerWasm.Trigger(code, used, fieldInitializers, operand, hostImports);
                    code[used++] = 0x24; // global.set
                    used = WasmBinary.Unsigned(code, used, operand);
                }
                else if (kind == TakeValueFieldAddress)
                {
                    int descriptor = records[slot + 3], storage = FieldAccess.Storage(descriptor);
                    int reference = Spill(spills, maximum, spillFlags, states[state + top], top);
                    used = GuardNull(code, used, reference);
                    used = ManagedValueLocationWasm.Take(code, used, FieldAccess.Owner(descriptor), reference, operand);
                    used = Local(code, used, 0x21, Spill(spills, maximum, spillFlags, ValueLocation(storage), top));
                }
                else if (kind == LoadConstructorAddress)
                {
                    // The verifier owns the address. The i32 placeholder is never dereferenced.
                    used = Const(code, used, 0);
                    used = Local(code, used, 0x21, Spill(spills, maximum, spillFlags, ConstructorAddress, depth));
                }
                else if (kind == LoadValueLocation)
                {
                    used = ManagedValueLocationWasm.ReadLocation(code, used, 0, operand);
                    used = Local(code, used, 0x21, Spill(spills, maximum, spillFlags, operand, depth));
                }
                else if ((kind == GetField || fieldStore || kind == FieldAddress) &&
                    records.Length > slot + 2 * BranchIl.Width + 3 && records[slot + 2 * BranchIl.Width + 2] != 0 &&
                    IsNativeInteger(states[state + (fieldStore ? top - 1 : top)]))
                {
                    // Stage 143 (decision 0071): p->F in memory 0. The field offset is the memarg offset.
                    int offset = records[slot + 2 * BranchIl.Width + 2] - 1, load = records[slot + 2 * BranchIl.Width + 3];
                    int receiver = fieldStore ? top - 1 : top;
                    int address = Spill(spills, maximum, spillFlags, Native, receiver);
                    if (kind == FieldAddress)
                    {
                        // &p->F does not dereference p: plain address arithmetic, as on the CLR.
                        used = Local(code, used, 0x20, address);
                        used = Const(code, used, offset);
                        code[used++] = 0x6a;
                        used = Local(code, used, 0x21, Spill(spills, maximum, spillFlags, Native, top));
                    }
                    else
                    {
                        used = LinearMemoryWasm.GuardNull(code, used, address);
                        used = Local(code, used, 0x20, address);
                        if (kind == GetField)
                        {
                            used = LinearMemoryWasm.Load(code, used, load, offset);
                            used = Local(code, used, 0x21, Spill(spills, maximum, spillFlags, StackDepths.IndirectType(load), top));
                        }
                        else
                        {
                            int value = states[state + top];
                            used = Local(code, used, 0x20, Spill(spills, maximum, spillFlags, value, top));
                            used = LinearMemoryWasm.Store(code, used, StoreFor(load), offset);
                        }
                    }
                }
                else if (kind == GetField || kind == SetField)
                {
                    int descriptor = records[slot + 3];
                    int type = FieldAccess.Owner(descriptor);
                    int storage = FieldAccess.Storage(descriptor);
                    int address = kind == GetField ? top : top - 1;
                    int reference = states[state + address] == ConstructorAddress ? 0 :
                        Spill(spills, maximum, spillFlags, states[state + address], address);
                    if (ManagedValueLocationWasm.UsesCell(objects, type, storage))
                    {
                        used = GuardNull(code, used, reference);
                        if (kind == GetField)
                        {
                            used = ManagedValueLocationWasm.Read(code, used, type, reference, operand, storage);
                            used = Local(code, used, 0x21, Spill(spills, maximum, spillFlags, StackType(storage), top));
                        }
                        else
                        {
                            int source = states[state + top];
                            used = ManagedValueLocationWasm.Write(code, used, type, reference, operand,
                                Spill(spills, maximum, spillFlags, source, top), source, storage);
                        }
                    }
                    else
                    {
                        if (IsValueType(objects, type))
                            used = ValueFieldsWasm.Materialize(code, used, type, reference);
                        else
                        {
                            used = GuardNull(code, used, reference);
                            used = Local(code, used, 0x20, reference);
                        }
                        if (kind == SetField)
                        {
                            int source = states[state + top];
                            used = LoadOperand(code, used, Spill(spills, maximum, spillFlags, source, top), source, storage);
                        }
                        if (IsExplicitValue(objects, type))
                            used = ExplicitValueWasm.Load(code, used, objects, type, operand, storage, Spill(spills, maximum, spillFlags, I64, top));
                        else
                        {
                            // Packed cells truncate on assignment. The load selects the CLI extension.
                            int instruction = kind == GetField ? FieldAccess.LoadInstruction(storage) : 5;
                            used = Gc(code, used, instruction, type);
                            used = WasmBinary.Unsigned(code, used, operand);
                        }
                        if (kind == GetField) used = Local(code, used, 0x21, Spill(spills, maximum, spillFlags, StackType(storage), top));
                    }
                }
                else if (kind == ClearValue)
                {
                    if (inlineLocations[operand] != 0)
                        used = InlineObjectStorageWasm.Clear(code, used, inlineLocations[operand], operand, FieldCount(objects, frame[operand]));
                    else if (frame[operand] == PeLibrary.RuntimeTypeMetadata.Handle)
                    {
                        code[used++] = 0xd0; // ref.null eq
                        code[used++] = 0x6d;
                        used = Local(code, used, 0x21, operand);
                    }
                    else used = ValueFieldsWasm.Clear(code, used, frame[operand], operand);
                }
                else if (kind == UpdateValue || kind == UpdateConstructorField)
                {
                    int descriptor = records[slot + 3];
                    int type = FieldAccess.Owner(descriptor);
                    int local = kind == UpdateConstructorField ? 0 : records[slot + BranchIl.Width + 1] - 1;
                    int source = states[state + top];
                    int replacement = Spill(spills, maximum, spillFlags, source, top);
                    if (inlineLocations[local] != 0)
                        used = InlineObjectStorageWasm.Set(code, used, inlineLocations[local], local, operand, replacement);
                    else if (IsExplicitValue(objects, type))
                        used = ExplicitValueWasm.Update(code, used, objects, type, local, operand, FieldAccess.Storage(descriptor), replacement, source);
                    else
                        used = ValueFieldsWasm.Update(code, used, objects, type, local, temporaries + type - FirstClass,
                            operand, replacement, source, IsValueLocation(frame[local]));
                }
                else if (kind == NewArray || kind == InitializedArray)
                {
                    int length = Spill(spills, maximum, spillFlags, states[state + top], top);
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
                    used = Local(code, used, 0x21, Spill(spills, maximum, spillFlags, operand, top));
                }
                else if (kind == ArrayLength)
                {
                    int reference = Spill(spills, maximum, spillFlags, states[state + top], top);
                    used = GuardNull(code, used, reference);
                    used = Local(code, used, 0x20, reference);
                    code[used++] = 0xfb;
                    code[used++] = 0x0f;
                    used = Local(code, used, 0x21, Spill(spills, maximum, spillFlags, Native, top));
                }
                else if (kind == ArrayGet)
                {
                    int array = operand == 0x9a ? states[state + top - 1] : ArrayLoadType(operand);
                    int reference = Spill(spills, maximum, spillFlags, states[state + top - 1], top - 1);
                    int index = Spill(spills, maximum, spillFlags, states[state + top], top);
                    used = GuardNull(code, used, reference);
                    used = GuardArrayIndex(code, used, reference, index, false);
                    used = Local(code, used, 0x20, reference);
                    used = Local(code, used, 0x20, index);
                    // The opcode selects the extension of packed cells. Full-
                    // width integer and floating arrays use plain array.get.
                    int instruction = array != Bytes && array != Chars ? 0x0b :
                        operand == 0x90 || operand == 0x92 ? 0x0c : 0x0d;
                    used = Gc(code, used, instruction, array);
                    int value = operand == 0x9a ? ArrayElement(objects, array) : ArrayValueType(array);
                    used = Local(code, used, 0x21, Spill(spills, maximum, spillFlags, value, top - 1));
                }
                else if (kind == ArraySet)
                {
                    int array = operand == 0xa2 ? states[state + top - 2] : ArrayStoreType(operand);
                    int reference = Spill(spills, maximum, spillFlags, states[state + top - 2], top - 2);
                    int index = Spill(spills, maximum, spillFlags, states[state + top - 1], top - 1);
                    used = GuardNull(code, used, reference);
                    used = GuardArrayIndex(code, used, reference, index, false);
                    used = Local(code, used, 0x20, reference);
                    used = Local(code, used, 0x20, index);
                    used = Local(code, used, 0x20, Spill(spills, maximum, spillFlags, states[state + top], top));
                    used = Gc(code, used, 0x0e, array);
                }

                if (kind == Return)
                {
                    // A value constructor's IL ret has an empty stack. Its Wasm
                    // result is the receiver's final aggregate.
                    if (valueConstructor) used = Local(code, used, 0x20, 0);
                    else if (result != Void)
                    {
                        int source = states[state + top];
                        used = LoadOperand(code, used, Spill(spills, maximum, spillFlags, source, top), source, result);
                        used = NormalizeSlot(code, used, result);
                    }
                    if (guard >= 0) used = TypeInitializerWasm.Complete(code, used, guard, activeInitializers);
                    if (stackPointer >= 0) used = StackAllocationWasm.Release(code, used, stackPointer, stackLimit, stackFrame);
                    code[used++] = 0x0f;
                }
                else if (kind == ThrowNull)
                {
                    int thrown = states[state + top];
                    if (thrown == NullValue)
                        used = ExceptionTagsWasm.Throw(code, used, ExceptionTagsWasm.NullTag);
                    else
                    {
                        // CLR throw of a null managed reference raises NullReferenceException.
                        // A non-null object travels on the object tag with its own reference.
                        int source = Spill(spills, maximum, spillFlags, thrown, top);
                        used = Local(code, used, 0x20, source);
                        code[used++] = 0xd1; // ref.is_null
                        used = ExceptionTagsWasm.ThrowIf(code, used, ExceptionTagsWasm.NullTag);
                        used = ExceptionDispatchWasm.ThrowObject(code, used, source);
                    }
                }
                else if (kind == Rethrow)
                    used = ExceptionDispatchWasm.ReraiseCaught(code, used, pcLocal + 2 + operand, objectBase + 1 + operand);
                else
                {
                    if (hasNull)
                        used = NullSpillWasm.Transfer(code, used, records, pc, depths, states, outgoingNulls, maximum, spills, spillFlags);
                    if (kind == Conditional)
                    {
                        int comparison = records[slot + 3];
                        used = Const(code, used, operand);
                        used = Const(code, used, next);
                        int type = states[state + top];
                        bool compares = IsComparison(comparison);
                        int leftType = compares ? states[state + top - 1] : type;
                        if (type == NullPointer || leftType == NullPointer)
                            used = Const(code, used, comparison == 0x45 || comparison == 0x46 ? 1 : 0);
                        else
                        {
                            int width = compares && IsFloat(type) ? FloatWidth(leftType, type) : type;
                            if (compares)
                                used = NumericLocal(code, used, Spill(spills, maximum, spillFlags, leftType, top - 1), leftType, width);
                            used = NumericLocal(code, used, Spill(spills, maximum, spillFlags, type, top), type, width);
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
                        used = FinallyWasm.Finish(code, used, pcLocal + 2 + operand, objectBase + 1 + operand, continuationBase + operand);
                    else if (kind == Leave && continuations != 0)
                        used = FinallyWasm.Leave(code, used, pc, operand, pcLocal, continuationBase, objectBase, catchRegion);
                    else used = Const(code, used, kind == Branch || kind == Leave ? operand : next);
                    used = Local(code, used, 0x21, pcLocal);
                    code[used++] = 0x0c;
                    // Catch dispatch adds try_table and one block per tag around the instruction if.
                    code[used++] = catchRegion.Length == 0 ? (byte)1 : (byte)ExceptionDispatchWasm.Depth;
                }
                code[used++] = 0x0b;
            }
            code[used++] = 0;
            if (catchRegion.Length != 0) used = ExceptionDispatchWasm.End(code, used, pcLocal, catchRegion, exceptionLocals > 1, objectBase, filters);
            code[used++] = 0x0b;
            code[used++] = 0;
            if (guard >= 0) used = TypeInitializerWasm.End(code, used, guard, epoch, activeInitializers);
            if (stackPointer >= 0)
            {
                code[used++] = 0x0b; // end try_table
                code[used++] = 0x00;
                code[used++] = 0x0b; // end block: the caught exnref is on the stack
                used = StackAllocationWasm.Release(code, used, stackPointer, stackLimit, stackFrame);
                code[used++] = 0x0a; // throw_ref
            }
            code[used++] = 0x0b;
            return used;
        }

        internal static int GuardNull(byte[] code, int used, int reference, int tag = ExceptionTagsWasm.NullTag)
        {
            used = Local(code, used, 0x20, reference);
            code[used++] = 0xd1; // ref.is_null
            return ExceptionTagsWasm.ThrowIf(code, used, tag);
        }

        internal static int GuardArrayIndex(byte[] code, int used, int reference, int index, bool wrapped)
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

        private static int NumericLocal(byte[] code, int used, int local, int source, int width)
        {
            used = Local(code, used, 0x20, local);
            if (source == F32 && width == F64) code[used++] = 0xbb;
            return used;
        }

        internal static byte NumericOpcode(int instruction, int type) =>
            (byte)(type == I64 ? instruction + (IsComparison(instruction) ? 0x0b : 0x12) : instruction);

        internal static int IsMinAndMinusOne(byte[] code, int used, int type, int left, int right)
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

        internal static int Spill(int first, int maximum, int spillFlags, int type, int depth) =>
            type == NullValue ? first - 1 : first + SpillBank(type, spillFlags) * maximum + depth;

        private static int ExtraSpillBanks(int flags) => (flags & LocationSpill) + (flags >> 1);
        // stind.* matching a field's ldind.* opcode.
        private static int StoreFor(int load) =>
            load <= 0x47 ? 0x52 : load <= 0x49 ? 0x53 : load <= 0x4b ? 0x54 : load == 0x4c ? 0x55 :
            load == 0x4e ? 0x56 : load == 0x4f ? 0x57 : 0xdf;


        // The gap between primitive tags and layout IDs has no Wasm locals.
        private static int SpillBank(int type, int flags) =>
            // NativeZero already spills as Native. Use its otherwise vacant bank
            // for native arrays without increasing the engine's local pressure.
            type == ConstructorAddress ? I32 - 1 :
            PeLibrary.RuntimeTypeMetadata.IsStorage(type) || IsValueLocation(type) ? ObjectReference - 1 :
            // A base Delegate slot currently accepts EventHandler lists only.
            // Both types have the same physical storage but distinct CLI stack identities.
            type == DelegateReference ? EventHandlerReference - 1 :
            IsManagedLocation(type) ? LastScalarBank :
            IsSpan(type) ? LastScalarBank + (flags & LocationSpill) :
            type >= FirstClass ? LastScalarBank + ExtraSpillBanks(flags) + type - FirstClass :
                (type == NativeZero ? Native : type == NativeVector ? NativeZero : type) - 1;

        internal static int LoadOperand(byte[] code, int used, int local, int source, int target)
        {
            if (IsValueLocation(source))
            {
                // Cell operands spill as eqref. Restore their physical type at a typed call.
                used = Local(code, used, 0x20, local);
                code[used++] = 0xfb;
                code[used++] = 0x17; // ref.cast null
                return WasmBinary.Signed(code, used, WasmBinary.ValueLocationHeap);
            }
            if (source != NativeZero || target != NullPointer) return Local(code, used, 0x20, local);
            code[used++] = 0xd0; // ref.null none
            code[used++] = NullSpillWasm.NullRef;
            return used;
        }

        internal static int NormalizeSlot(byte[] code, int used, int type)
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
            return WasmBinary.Unsigned(code, used, WasmBinary.HeapType(type));
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
