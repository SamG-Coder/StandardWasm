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
            byte[] outgoingNulls, int[] outgoingReferences, int[] objects, int[] fieldInitializers, RuntimeReflectionPlan reflection,
            int[] inlineLocations, int[] nativePlans, int[] filters, int[] frameCells,
            FunctionPointerPlan pointers, int rootCount, byte[] frameRoots, FinalizerSlots finalizers, int[] failure,
            PrimitiveArrayWasm primitiveArrays, ValueFieldProjections projections, byte[] literals)
        {
            int arguments = frameShape[0], locals = frameShape[1], maximum = frameShape[2];
            int result = frameShape[3];
            int classes = frameShape[4];
            bool stringScratch = (frameShape[5] & 1) != 0;
            bool scopedPinScratch = (frameShape[5] & 2) != 0;
            bool enumScratch = (frameShape[5] & 4) != 0;
            int scopedValuePin = -1;
            if ((frameShape[5] & ScopedValuePins.FrameFlag) != 0)
                for (int at = 0; at < records.Length; at += Width)
                    if (records[at] == PinScopedValue) { scopedValuePin = at; break; }
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
            // Stage 149 (decision 0077): a pinning frame also saves the live-pin count.
            int pins = frameShape[24] - 1;
            bool tracksRoots = frameRoots != null;
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
            int cellCount = 0;
            for (int i = 0; i < frameCells.Length; i++)
            {
                if (frameCells[i] == 0) continue;
                cellCount++;
            }
            int spills = arguments + locals + (hasNull ? 1 : 0);
            int fixedLocals = spills + classes + (stringScratch ? 5 : 0) + (enumScratch ? 2 : 0) +
                (eventScratch ? 6 : 0) + (scopedPinScratch ? 8 : 0) + (resourceFunction >= 0 ? 7 : 0) + 1 + 2 * exceptionLocals + continuations +
                (stackPointer >= 0 ? 1 : 0) + (pins >= 0 ? 1 : 0) + (staticRegion >= 0 ? 1 : 0) + cellCount + (tracksRoots ? 1 : 0) + (failure != null ? 1 : 0);
            if (fixedLocals > MaxLocals || maximum > (MaxLocals - fixedLocals) / spillKinds) return -2;
            int temporaries = spills + spillKinds * maximum;
            int scratch = temporaries + classes;
            int enumLocals = scratch + (stringScratch ? 5 : 0);
            int eventLocals = enumLocals + (enumScratch ? 2 : 0);
            int scopedPinLocals = eventLocals + (eventScratch ? 6 : 0);
            // Decision 0105: proved alias-free pins, their per-site block locals (declared last) and
            // the heap scratch they share.
            int pinSites = PinnedArraysWasm.CountPins(records);
            var pinScratch = new int[NativeHeapWasm.ScratchCount];
            for (int i = 0; i < 5; i++) pinScratch[i] = eventLocals + i;
            pinScratch[5] = scratch + 3;
            int resourceLocals = scopedPinLocals + (scopedPinScratch ? 8 : 0);
            int pcLocal = resourceLocals + (resourceFunction >= 0 ? 7 : 0);
            int continuationBase = pcLocal + 1 + exceptionLocals;
            // Thrown managed objects (decision 0054) mirror the exnref locals: a scratch
            // eqref followed by one saved object per clause.
            int objectBase = continuationBase + continuations;
            int used = WasmBinary.Unsigned(code, 0, locals + classes + (hasNull ? 1 : 0) + (maximum == 0 ? 1 : 1 + spillKinds) +
                (stringScratch ? 2 : 0) + (enumScratch ? 1 : 0) + (eventScratch ? 2 : 0) + (scopedPinScratch ? 2 : 0) + (resourceFunction >= 0 ? 3 : 0) + (exceptionLocals != 0 ? 1 : 0) +
                (continuations != 0 ? 1 : 0) + (exceptionLocals != 0 ? 1 : 0) + (stackPointer >= 0 ? 1 : 0) +
                (staticRegion >= 0 ? 1 : 0) + cellCount + (tracksRoots ? 1 : 0) + (failure != null ? 1 : 0) +
                (pinSites != 0 ? 1 : 0));
            // The saved shadow-stack pointer follows every other local.
            int stackFrame = objectBase + exceptionLocals, pinFrame = stackFrame + 1;
            int staticAddress = stackFrame + (stackPointer >= 0 ? 1 : 0) + (pins >= 0 ? 1 : 0);
            int rootFrame = staticAddress + (staticRegion >= 0 ? 1 : 0) + cellCount;
            if (failure != null) failure[TypeInitializerWasm.FailureInner] = rootFrame + (tracksRoots ? 1 : 0);
            int[] arrayPins = PinnedArrays.Pins(records, frame, rootFrame + (tracksRoots ? 1 : 0) + (failure != null ? 1 : 0));
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
                code[used++] = StackSpillWasm.NullRef;
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
            if (enumScratch)
            {
                code[used++] = 2;
                code[used++] = 0x7e; // invariant magnitude and digit workspace
            }
            if (eventScratch)
            {
                code[used++] = 5; // loop counters and copy lengths
                code[used++] = 0x7f;
                code[used++] = 1; // unpublished output array
                used = WasmBinary.ValueType(code, used, EventHandlerReference);
            }
            if (scopedPinScratch)
            {
                code[used++] = 1;
                used = WasmBinary.ValueType(code, used, Chars);
                code[used++] = 7; // array offset, extent, native address, and copy/stack scratch
                code[used++] = 0x7f;
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
                code[used++] = (byte)(pins >= 0 ? 2 : 1); // saved $sp, then the saved pin count
                code[used++] = 0x7f;
            }
            if (staticRegion >= 0)
            {
                code[used++] = 1;
                code[used++] = 0x7f;
            }
            if (cellCount != 0)
            {
                for (int i = 0; i < frameCells.Length; i++)
                {
                    if (frameCells[i] == 0) continue;
                    code[used++] = 1;
                    used = WasmBinary.ValueType(code, used, NativeFrameWasm.IsCell(frameCells[i]) ? Native : frameCells[i]);
                }
            }
            if (tracksRoots)
            {
                code[used++] = 1;
                code[used++] = 0x7f;
            }
            if (failure != null)
            {
                code[used++] = 1;
                code[used++] = 0x6d; // eqref: the escaped managed object
            }
            if (arrayPins != null)
            {
                used = WasmBinary.Unsigned(code, used, arrayPins.Length / 4);
                code[used++] = 0x7f; // one live heap block per pin site (decision 0105)
            }
            int[] cellLocals = null;
            if (cellCount != 0)
            {
                cellLocals = new int[frameCells.Length];
                int cell = staticAddress + (staticRegion >= 0 ? 1 : 0);
                for (int i = 0; i < frameCells.Length; i++)
                {
                    if (frameCells[i] == 0) continue;
                    // Every load, store and address of this slot uses the same backing storage.
                    int vector = frameCells[i];
                    if (NativeFrameWasm.IsCell(vector))
                    {
                        cellLocals[i] = cell++;
                        continue;
                    }
                    if (i < arguments) used = Local(code, used, 0x20, i);
                    else if (vector == Longs) used = WideConst(code, used, 0, 0);
                    else if (vector == Bytes || vector == Chars || vector == Ints || vector == NativeVector) used = Const(code, used, 0);
                    else
                    {
                        code[used++] = 0xd0; // ref.null eq
                        code[used++] = 0x6d;
                    }
                    if (IsValueLocation(vector)) used = ManagedValueLocationWasm.CreateCell(code, used, tracksRoots);
                    else
                    {
                        used = Gc(code, used, 8, vector); // array.new_fixed
                        code[used++] = 1;
                    }
                    used = Local(code, used, 0x21, cell);
                    cellLocals[i] = cell++;
                }
            }
            if (tracksRoots)
            {
                used = RootStackWasm.SaveBase(code, used, rootCount, rootFrame);
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
            if (prologue >= 0 && !tracksRoots)
                used = TypeInitializerWasm.CallInitializer(code, used, hostImports + prologue);
            if (guard >= 0) used = TypeInitializerWasm.Begin(code, used, guard, epoch, activeInitializers, pcLocal,
                tracksRoots ? rootCount : -1, failure);
            if (stackPointer >= 0)
            {
                used = StackAllocationWasm.Begin(code, used, stackPointer, stackFrame);
                if (pins >= 0) used = PinRegistryWasm.Begin(code, used, pins, pinFrame);
            }
            if (tracksRoots || stackPointer >= 0 || arrayPins != null)
            {
                // A single cleanup region owns the frame's roots, pins and native allocations.
                code[used++] = 0x02; code[used++] = ExceptionDispatchWasm.ExceptionReference; // block (result exnref)
                code[used++] = 0x1f; code[used++] = 0x40; code[used++] = 1;                  // try_table
                code[used++] = 0x03; code[used++] = 0;                                         // catch_all_ref 0
            }
            if (stackPointer >= 0 && cellCount != 0)
                used = NativeFrameWasm.InitializeSlots(code, used, stackPointer, stackBase, stackLimit, pcLocal, arguments, frameCells, cellLocals, nativePlans);
            if (prologue >= 0 && tracksRoots)
                used = RootStackWasm.CallInitializerWithRoots(code, used, rootCount, rootFrame, frameRoots,
                    arguments, cellLocals, hostImports + prologue);
            code[used++] = 0x03;
            code[used++] = 0x40;
            if (catchRegion.Length != 0) used = ExceptionDispatchWasm.Begin(code, used);
            // The dispatch loop tests one state per `if`, in pc order. A large body groups
            // consecutive states into blocks headed by `br_if (pc >= next group's first pc)`,
            // so re-entering the loop skips a whole group with one comparison instead of
            // testing each of its states (#316). A branch to the loop leaves one more block.
            int emitted = 0;
            for (int pc = 0; pc < depths.Length; pc++)
                if (depths[pc] != 0) emitted++;
            int groupSize = emitted > MinimumGroupedStates ? GroupSize(emitted) : 0;
            int loopDepth = (catchRegion.Length == 0 ? 1 : ExceptionDispatchWasm.Depth) + (groupSize != 0 ? 1 : 0);
            int ordinal = 0;
            for (int pc = 0; pc < depths.Length; pc++)
            {
                if (depths[pc] == 0) continue;
                if (groupSize != 0 && ordinal % groupSize == 0)
                {
                    if (ordinal != 0) code[used++] = 0x0b; // end the previous group
                    used = BeginGroup(code, used, depths, pc, groupSize, pcLocal);
                }
                ordinal++;
                int slot = pc * BranchIl.Width;
                int kind = records[slot], operand = records[slot + 1], next = records[slot + 2];
                int depth = depths[pc] - 1;
                int state = pc * maximum;
                int top = depth - 1;
                bool fallsThrough = false;
                bool fieldStore = kind == SetField || kind == UpdateConstructorField;
                used = Local(code, used, 0x20, pcLocal);
                used = Const(code, used, pc);
                code[used++] = 0x46;
                code[used++] = 0x04;
                code[used++] = 0x40;

                int tracked = 0;
                bool publishesRoots = tracksRoots && (kind == Call || kind == NewObject || kind == ConstructValue ||
                    kind == ResourceLookup || kind == InvokeDelegate || kind == CallNativeIndirect || kind == CallComCookieLookup ||
                    kind == CallDynamicNative || kind == CallPinnedCharacterNative ||
                    // Decision 0104: Collect marks from the root stack; finalizers can allocate and collect.
                    kind == GarbageCollectorCall && operand != GarbageCollectorMembers.KeepAlive ||
                    TypeInitializerWasm.HasFieldTrigger(kind, operand, fieldInitializers));
                if (publishesRoots)
                {
                    int first = kind == Call || kind == NewObject || kind == ConstructValue
                        ? depth - arities[operand] + (kind == NewObject ? 1 : 0)
                        : kind == InvokeDelegate ? depth - (operand == Delegate ? 1 : 3) : depth;
                    int frameStart = pc * (arguments + locals);
                    for (int local = 0; local < arguments + locals; local++) tracked += frameRoots[frameStart + local];
                    if (exceptionLocals > 1) tracked += RootStackWasm.CountExceptionRoots(catchRegion, pc);
                    for (int index = 0; index < first; index++)
                        if (RootStackWasm.IsTraced(states[state + index])) tracked++;
                    if (kind == NewObject && !IsValueType(objects, signatures[offsets[operand] + 1])) tracked++;
                    if (tracked != 0)
                    {
                        used = RootStackWasm.Enter(code, used, rootCount, rootFrame, tracked);
                        int root = 0;
                        for (int local = 0; local < arguments + locals; local++)
                        {
                            if (frameRoots[frameStart + local] == 0) continue;
                            // An addressed local is updated through its stable cell, not the original Wasm local.
                            int backing = frameCells[local] == 0 ? local : cellLocals[local];
                            used = RootStackWasm.Store(code, used, rootFrame, root++, backing);
                        }
                        // The callee owns consumed arguments. This frame holds the surviving prefix.
                        for (int index = 0; index < first; index++)
                        {
                            int type = states[state + index];
                            if (RootStackWasm.IsTraced(type))
                                used = RootStackWasm.Store(code, used, rootFrame, root++, Spill(spills, maximum, spillFlags, type, index));
                        }
                        // Hidden payload locals follow the visible frame and stack roots.
                        if (exceptionLocals > 1)
                            used = RootStackWasm.PublishExceptionRoots(code, used, catchRegion, pc, rootFrame, root, objectBase);
                    }
                }

                if (kind == Load || kind == Constant || kind == WideConstant || (kind == Dup && states[state + top] != NullValue) ||
                    kind == SingleConstant || kind == DoubleConstant)
                {
                    int pushed = I32;
                    if (kind == Load)
                    {
                        pushed = StackType(frame[operand]);
                        if (frameCells[operand] == Native)
                            used = NativeFrameWasm.LoadWord(code, used, cellLocals[operand]);
                        else if (frameCells[operand] < 0)
                            used = NativeAggregates.Load(code, used, nativePlans, -frameCells[operand], cellLocals[operand], 0);
                        else if (IsValueLocation(frameCells[operand]))
                            used = ManagedValueLocationWasm.ReadLocation(code, used, cellLocals[operand], pushed);
                        else if (frameCells[operand] != 0)
                            used = ManagedLocationWasm.LoadCell(code, used, cellLocals[operand], frameCells[operand], Representation(objects, pushed));
                        else used = inlineLocations[operand] == 0 ? Local(code, used, 0x20, operand) :
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
                    reflection.ObjectTypes.Include(objects, type);
                    int receiver = Spill(spills, maximum, spillFlags, type, top);
                    used = GuardNull(code, used, receiver);
                    used = Local(code, used, 0x20, receiver);
                    code[used++] = 0x10; // call the type lookup after all layouts are linked
                    used = WasmIndexFixups.Write(code, used, frameShape[7], reflection.ObjectTypes.Fixups);
                    used = Local(code, used, 0x21, Spill(spills, maximum, spillFlags, PeLibrary.RuntimeTypeMetadata.Type, top));
                }
                else if (kind == RuntimeTypeDisplayName || kind == ReadReflection && PeLibrary.ReflectionMembers.IsTypeText(operand))
                {
                    int receiver = Spill(spills, maximum, spillFlags, PeLibrary.RuntimeTypeMetadata.Type, top);
                    used = GuardNull(code, used, receiver);
                    used = Local(code, used, 0x20, receiver);
                    code[used++] = 0x10;
                    int form = kind == RuntimeTypeDisplayName ? RuntimeTypeNames.Display :
                        operand == PeLibrary.ReflectionMembers.TypeName ? RuntimeTypeNames.Simple : RuntimeTypeNames.Namespace;
                    int[] fixups = reflection.TypeTextFixups(form);
                    used = WasmIndexFixups.Write(code, used, frameShape[7], fixups);
                    used = Local(code, used, 0x21, Spill(spills, maximum, spillFlags, String, top));
                }
                else if (kind == RuntimeTypeIsGeneric || kind == RuntimeTypeGenericDefinition ||
                    kind == RuntimeTypeGenericArguments || kind == RuntimeTypeIsEnum || kind == RuntimeTypeIsValue)
                {
                    int receiver = Spill(spills, maximum, spillFlags, states[state + top], top);
                    used = GuardNull(code, used, receiver);
                    used = Local(code, used, 0x20, receiver);
                    code[used++] = 0x10;
                    bool predicate = kind == RuntimeTypeIsGeneric || kind == RuntimeTypeIsEnum || kind == RuntimeTypeIsValue;
                    int[] fixups = predicate ? reflection.TypePredicateFixups(kind) :
                        kind == RuntimeTypeGenericDefinition ? reflection.GenericDefinitions.Fixups : reflection.GenericArguments.Fixups;
                    used = WasmIndexFixups.Write(code, used, frameShape[7], fixups);
                    int resultType = predicate ? I32 :
                        kind == RuntimeTypeGenericDefinition ? PeLibrary.RuntimeTypeMetadata.Type : operand;
                    used = Local(code, used, 0x21, Spill(spills, maximum, spillFlags, resultType, top));
                }
                else if (kind == EnumHasFlag)
                {
                    int flag = Spill(spills, maximum, spillFlags, operand, top);
                    used = Local(code, used, 0x20, Spill(spills, maximum, spillFlags, operand, top - 1));
                    used = Local(code, used, 0x20, flag);
                    code[used++] = operand == I64 ? (byte)0x83 : (byte)0x71; // and
                    used = Local(code, used, 0x20, flag);
                    code[used++] = operand == I64 ? (byte)0x51 : (byte)0x46; // eq
                    used = Local(code, used, 0x21, Spill(spills, maximum, spillFlags, I32, top - 1));
                }
                else if (kind == JoinManyStrings)
                {
                    // Concat(a, b, c[, d]) == Concat(Concat(Concat(a, b), c), d) for both content and
                    // identity: each step returns the other operand when one side is null or empty, so a
                    // single non-empty argument is returned as-is and only two or more allocate.
                    int at = top - records[slot + 3] + 1;
                    int accumulator = Spill(spills, maximum, spillFlags, states[state + at], at);
                    for (int i = 1; i < records[slot + 3]; i++)
                    {
                        used = StringWasm.Copy(code, used, accumulator,
                            Spill(spills, maximum, spillFlags, states[state + at + i], at + i), scratch, true, operand);
                        accumulator = Spill(spills, maximum, spillFlags, String, at);
                        used = Local(code, used, 0x21, accumulator);
                    }
                }
                else if (kind == JoinStringArray)
                {
                    int vector = PeLibrary.ObjectLayouts.FindArray(objects, String);
                    used = StringArrayJoinWasm.Emit(code, used, vector, Spill(spills, maximum, spillFlags, states[state + top], top),
                        operand, scratch, scratch + 1, scratch + 3, scratch + 4, enumLocals, enumLocals + 1);
                    used = Local(code, used, 0x21, Spill(spills, maximum, spillFlags, String, top));
                }
                else if (kind == FormatPrimitive)
                {
                    // [location] -> [String]: read the receiver through its managed location as
                    // ldind.i4/ldind.i8 would, widen to the i64 magnitude workspace, then format.
                    int type = states[state + top];
                    int address = Spill(spills, maximum, spillFlags, type, top);
                    bool wide = operand >= 10;
                    // A folded receiver (ldloca/ldflda then ToString) is already the value.
                    if (records[slot + 3] == PrimitiveFormatting.ValueReceiver) used = Local(code, used, 0x20, address);
                    else used = ValueFieldProjectionWasm.Read(code, used, frameShape[7], address,
                        wide ? Longs : operand == 5 ? Bytes : Ints, type, projections);
                    // Small and 32-bit stack values are already sign- or zero-extended to i32.
                    if (!wide) code[used++] = (operand & 1) == 0 ? (byte)0xac : (byte)0xad; // i64.extend_i32_s/u
                    used = Local(code, used, 0x21, enumLocals);
                    used = EnumFormattingWasm.FormatDecimal(code, used, (operand & 1) == 0, scratch, scratch + 3, scratch + 4,
                        enumLocals, enumLocals + 1);
                    used = Local(code, used, 0x21, Spill(spills, maximum, spillFlags, String, top));
                }
                else if (kind == FormatEnum)
                {
                    int element = records[slot + 3];
                    used = EnumFormattingWasm.Emit(code, used, literals, operand, element,
                        Spill(spills, maximum, spillFlags, EnumFormatting.StackStorage(element), top),
                        Spill(spills, maximum, spillFlags, String, top), scratch, scratch + 3, scratch + 4,
                        enumLocals, enumLocals + 1);
                }
                else if (kind == EqualRuntimeDescriptors)
                {
                    int receiver = Spill(spills, maximum, spillFlags, states[state + top - 1], top - 1);
                    if (operand == PeLibrary.RuntimeTypeMetadata.Assembly) used = GuardNull(code, used, receiver);
                    used = Local(code, used, 0x20, receiver);
                    used = Local(code, used, 0x20, Spill(spills, maximum, spillFlags, states[state + top], top));
                    code[used++] = 0xd3; // ref.eq on canonical descriptors
                    if (records[slot + 3] != 0) code[used++] = 0x45; // i32.eqz for inequality
                    used = Local(code, used, 0x21, Spill(spills, maximum, spillFlags, I32, top - 1));
                }
                // TypeFromHandle and TypeToHandle change the CLI category and keep the descriptor in its shared spill.
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
                else if (kind == LoadStaticByteSpan)
                {
                    used = SpanWasm.FromStaticData(code, used, operand, records[slot + 3], records[slot + Width + 1], Bytes, ReadOnlyByteSpan);
                    used = Local(code, used, 0x21, Spill(spills, maximum, spillFlags, ReadOnlyByteSpan, depth));
                }
                else if (kind == LoadStaticPrimitiveSpan)
                {
                    int count = records[slot + Width + 1], vector = records[slot + Width + 2], span = records[slot + 3];
                    used = SpanWasm.FromStaticData(code, used, operand, count, count, vector, span);
                    used = Local(code, used, 0x21, Spill(spills, maximum, spillFlags, span, depth));
                }
                else if (kind == TakeStaticByteLocation || kind == TakeCharacterSpanLocation || kind == TakeReadOnlyCharacterLocation)
                {
                    int viewType = kind == TakeCharacterSpanLocation ? WritableCharacterSpan :
                        kind == TakeReadOnlyCharacterLocation ? CharacterSpan : ReadOnlyByteSpan;
                    int locationType = kind == TakeCharacterSpanLocation ? CharacterLocation :
                        kind == TakeReadOnlyCharacterLocation ? ReadOnlyCharacterLocation : StaticByteLocation;
                    int view = Spill(spills, maximum, spillFlags, viewType, top);
                    used = SpanWasm.TakeLocation(code, used, view, locationType);
                    used = Local(code, used, 0x21, Spill(spills, maximum, spillFlags, locationType, top));
                }
                else if (kind == OffsetStaticByteLocation || kind == OffsetCharacterLocation)
                {
                    bool characters = kind == OffsetCharacterLocation;
                    int locationType = states[state + top - 1];
                    int target = Spill(spills, maximum, spillFlags, locationType, top - 1);
                    int offset = Spill(spills, maximum, spillFlags, states[state + top], top);
                    used = !characters ? ManagedLocationWasm.OffsetStaticByte(code, used, target, offset) :
                        records[slot + 3] == StaticByteIntrinsics.CharacterElementOffset
                            ? ManagedLocationWasm.OffsetCharacterElements(code, used, target, offset)
                            : ManagedLocationWasm.OffsetCharacter(code, used, target, offset);
                    used = Local(code, used, 0x21, target);
                }
                else if (kind == ReadStaticUInt16)
                {
                    used = ManagedLocationWasm.ReadStaticUInt16(code, used, Spill(spills, maximum, spillFlags, StaticByteLocation, top));
                    used = Local(code, used, 0x21, Spill(spills, maximum, spillFlags, I32, top));
                }
                else if (kind == LoadNativeMessage)
                {
                    used = StringWasm.AllocateLiteral(code, used, operand, records[slot + 3]);
                    used = Local(code, used, 0x21, Spill(spills, maximum, spillFlags, String, depth));
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
                    if (operand == 1)
                    {
                        // [left, right] -> [!equal] for String.op_Inequality.
                        used = Local(code, used, 0x20, spills + top - 1);
                        code[used++] = 0x45; // i32.eqz
                        used = Local(code, used, 0x21, spills + top - 1);
                    }
                }
                else if (kind == JoinStrings || kind == StringFromChars)
                {
                    bool join = kind == JoinStrings;
                    int at = top - (join ? 1 : 0);
                    used = StringWasm.Copy(code, used, Spill(spills, maximum, spillFlags, states[state + at], at),
                        Spill(spills, maximum, spillFlags, states[state + top], top), scratch, join, operand);
                    used = Local(code, used, 0x21, Spill(spills, maximum, spillFlags, String, at));
                }
                else if (kind == StringFirstCodeUnit)
                {
                    int reference = Spill(spills, maximum, spillFlags, states[state + top], top);
                    used = GuardNull(code, used, reference);
                    used = StringWasm.FirstCodeUnit(code, used, reference);
                    used = Local(code, used, 0x21, spills + top);
                }
                else if (kind == StringFromSpan)
                {
                    int span = Spill(spills, maximum, spillFlags, records[slot + 3], top);
                    used = StringWasm.CopySpan(code, used, span, operand, scratch, frameShape[25] != 0);
                    used = Local(code, used, 0x21, Spill(spills, maximum, spillFlags, String, top));
                }
                else if (kind == AllocateString)
                {
                    used = StringWasm.AllocateForConstruction(code, used, Spill(spills, maximum, spillFlags, states[state + top], top));
                    used = Local(code, used, 0x21, Spill(spills, maximum, spillFlags, String, top));
                }
                else if (kind == NativeUtf16Length)
                    used = Utf16MemoryWasm.Length(code, used, Spill(spills, maximum, spillFlags, Native, top),
                        scratch + 3, Spill(spills, maximum, spillFlags, I32, top));
                else if (kind == ScalarBitCast)
                {
                    int target = records[slot + 3];
                    used = Local(code, used, 0x20, Spill(spills, maximum, spillFlags, operand, top));
                    code[used++] = (byte)CoreScalarBitCast.Opcode(operand, target);
                    used = Local(code, used, 0x21, Spill(spills, maximum, spillFlags, target, top));
                }
                else if (kind == CopyNativeConstructionString)
                    used = Utf16MemoryWasm.CopyIntoConstruction(code, used, Spill(spills, maximum, spillFlags, String, top - 2),
                        Spill(spills, maximum, spillFlags, Native, top - 1), Spill(spills, maximum, spillFlags, Native, top),
                        scratch, scratch + 3);
                else if (kind == CopyConstructionString)
                    used = StringWasm.CopyForConstruction(code, used, Spill(spills, maximum, spillFlags, String, top - 2),
                        Spill(spills, maximum, spillFlags, I32, top - 1), Spill(spills, maximum, spillFlags, String, top), scratch);
                else if (kind == StringConstructionSpan)
                {
                    used = StringWasm.OpenConstructionSpan(code, used, Spill(spills, maximum, spillFlags, String, top - 1), spills + top);
                    used = Local(code, used, 0x21, Spill(spills, maximum, spillFlags, WritableCharacterSpan, top - 1));
                }
                else if (kind == CopyCharacterSpan)
                {
                    used = CharacterSpanCopyWasm.Copy(code, used,
                        Spill(spills, maximum, spillFlags, records[slot + 3], top - 1),
                        Spill(spills, maximum, spillFlags, WritableCharacterSpan, top),
                        scratch + 3, scratch + 4, frameShape[25] != 0);
                }
                else if (kind == FindCharacterInSpan)
                {
                    int view = top - operand + 1;
                    used = CharacterSpanSearchWasm.Find(code, used,
                        Spill(spills, maximum, spillFlags, CharacterSpan, view),
                        Spill(spills, maximum, spillFlags, I32, view + 1),
                        operand == 3 ? Spill(spills, maximum, spillFlags, I32, top) : -1,
                        Spill(spills, maximum, spillFlags, I32, view),
                        scratch + 3, scratch + 4, frameShape[25] != 0);
                }
                else if (kind == StringRange)
                {
                    int source = Spill(spills, maximum, spillFlags, String, top - 2);
                    int start = Spill(spills, maximum, spillFlags, I32, top - 1);
                    int length = Spill(spills, maximum, spillFlags, I32, top);
                    used = GuardNull(code, used, source);
                    used = StringWasm.CopyRange(code, used, source, start, length, scratch);
                    used = Local(code, used, 0x21, source);
                }
                else if (kind == StringLength || kind == StringCharacter || kind == StringContainsCodeUnit)
                {
                    int at = top - (kind == StringLength ? 0 : 1);
                    int reference = Spill(spills, maximum, spillFlags, states[state + at], at);
                    used = GuardNull(code, used, reference);
                    if (kind == StringContainsCodeUnit)
                        used = StringWasm.ContainsCodeUnit(code, used, reference, spills + top, spills + at, scratch);
                    else
                    {
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
                }
                else if (kind == NativePresent)
                {
                    // Stage 156 (decision 0083): present(entry) -> i32, both indices absolute.
                    used = Const(code, used, records[slot + 3]);
                    code[used++] = 0x10;
                    used = WasmBinary.Unsigned(code, used, operand);
                    used = Local(code, used, 0x21, Spill(spills, maximum, spillFlags, I32, depth));
                }
                else if (kind == NativeHelper)
                {
                    code[used++] = 0x10; // () -> () host helper, absolute index
                    used = WasmBinary.Unsigned(code, used, operand);
                }
                else if (kind == CallNative)
                {
                    // The thunk's own arguments, in order, to the typed import. Narrow integer
                    // results are normalized to their CLI width, as a returned managed value is.
                    int arity = arities[operand], shape = offsets[operand];
                    int first = depth - arity;
                    for (int i = 0; i < arity; i++)
                    {
                        int source = states[state + first + i];
                        used = LoadOperand(code, used, Spill(spills, maximum, spillFlags, source, first + i), source, signatures[shape + 1 + i]);
                    }
                    code[used++] = 0x10;
                    used = WasmBinary.Unsigned(code, used, records[slot + 3]);
                    int returned = signatures[shape];
                    if (returned != Void)
                    {
                        used = NormalizeSlot(code, used, returned);
                        used = Local(code, used, 0x21, Spill(spills, maximum, spillFlags, StackType(returned), first));
                    }
                }
                else if (kind == ConsoleReadLine)
                {
                    // The String scratch bank supplies the Char[] and two i32 locals.
                    used = ConsoleInputWasm.ReadLine(code, used, frameShape[26] - 1, scratch, scratch + 3, scratch + 4,
                        Spill(spills, maximum, spillFlags, String, depth));
                }
                else if (kind == ConsoleWriter)
                {
                    used = Const(code, used, operand); // host stream number
                    used = Local(code, used, 0x21, Spill(spills, maximum, spillFlags, PeLibrary.ConsoleStorage.Writer, depth));
                }
                else if (kind == ConsoleWrite)
                {
                    bool text = operand != PeLibrary.ConsoleMetadata.WriteEmptyLine && operand != PeLibrary.ConsoleMetadata.Flush;
                    int writer = text ? top - 1 : top;
                    // The String scratch bank supplies a Char[] and two i32 locals.
                    used = ConsoleWasm.Write(code, used, operand,
                        Spill(spills, maximum, spillFlags, PeLibrary.ConsoleStorage.Writer, writer),
                        text ? Spill(spills, maximum, spillFlags, states[state + top], top) : 0,
                        scratch, scratch + 3, scratch + 4);
                }
                else if (kind == LoadVirtualFunction)
                {
                    // ldvirtftn on a null object raises NullReferenceException, as on the CLR. The
                    // thunk (linked to the method's dispatcher) selects the override at invocation.
                    used = GuardNull(code, used, Spill(spills, maximum, spillFlags, states[state + top], top));
                    used = Const(code, used, operand);
                    used = Local(code, used, 0x21, Spill(spills, maximum, spillFlags, MethodPointer, top));
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
                    // The target's own stack type may be a subclass of the receiver (StackDepths).
                    int target = Spill(spills, maximum, spillFlags, states[state + top - 1], top - 1);
                    // A static callback carries a null target. An instance callback
                    // uses the receiver validated by the delegate construction rule.
                    if (receiver != NullValue) used = GuardNull(code, used, target, ExceptionTagsWasm.ArgumentTag);
                    used = Local(code, used, 0x20, target);
                    if (receiver != NullValue) code[used++] = 0xd4; // ref.as_non_null
                    int pointer = Spill(spills, maximum, spillFlags, MethodPointer, top);
                    int destination = Spill(spills, maximum, spillFlags, closureType, top - 1);
                    used = Local(code, used, 0x20, pointer);
                    code[used++] = 0x25; // table.get from the generated thunk table
                    code[used++] = 0;
                    if (closureType == EventHandlerReference)
                    {
                        used = CastThunk(code, used, WasmBinary.EventThunkType);
                        used = EventWasm.NewList(code, used, pointer, destination, 0);
                    }
                    else if (PeLibrary.DelegateShapes.IsKey(closureType))
                    {
                        code[used++] = 0xd4; // ref.as_non_null: the entry holds (ref func)
                        used = EventWasm.NewList(code, used, pointer, destination, DelegateCastWasm.Nominal(closureType));
                    }
                    else used = Gc(code, used, 0, closureType); // struct.new scalar closure
                    used = Local(code, used, 0x21, destination);
                }
                else if (kind == DelegateEquality)
                {
                    int left = Spill(spills, maximum, spillFlags, states[state + top - 1], top - 1);
                    int right = Spill(spills, maximum, spillFlags, states[state + top], top);
                    used = EventWasm.ListsEqual(code, used, left, right, eventLocals);
                    if (operand != 0) code[used++] = 0x45; // i32.eqz: op_Inequality
                    used = Local(code, used, 0x21, Spill(spills, maximum, spillFlags, I32, top - 1));
                }
                else if (kind == CombineHandler || kind == RemoveHandler)
                {
                    int left = Spill(spills, maximum, spillFlags, states[state + top - 1], top - 1);
                    int right = Spill(spills, maximum, spillFlags, states[state + top], top);
                    used = kind == CombineHandler ? EventWasm.CombineLists(code, used, left, right, eventLocals) :
                        EventWasm.RemoveLast(code, used, left, right, eventLocals);
                    used = Local(code, used, 0x21, Spill(spills, maximum, spillFlags, EventHandlerReference, top - 1));
                }
                else if (kind == InvokeDelegate && operand == CharacterSpanCallback)
                {
                    used = InvokeCharacterSpanCallback(code, used,
                        Spill(spills, maximum, spillFlags, CharacterSpanCallback, top - 2),
                        Spill(spills, maximum, spillFlags, WritableCharacterSpan, top - 1),
                        Spill(spills, maximum, spillFlags, Native, top));
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
                    // A store to a pinned array local ends its pin: copy the block back first.
                    if (arrayPins != null && records[slot + 3] == PinnedLocals.Array)
                        for (int site = 0; site < arrayPins.Length / 4; site++)
                            if (arrayPins[site * 4] == operand)
                                used = PinnedArraysWasm.Unpin(code, used, arrayPins, site, pinScratch, false);
                    int source = states[state + top];
                    int spilled = Spill(spills, maximum, spillFlags, source, top);
                    int type = frame[operand], backing = inlineLocations[operand];
                    if (frameCells[operand] == Native)
                        used = NativeFrameWasm.StoreWord(code, used, cellLocals[operand], spilled, source);
                    else if (frameCells[operand] < 0)
                        used = NativeFrameWasm.StoreAggregate(code, used, nativePlans, objects, -frameCells[operand],
                            cellLocals[operand], spilled, source, type, spills, maximum, spillFlags, top);
                    else if (IsValueLocation(frameCells[operand]))
                    {
                        used = Local(code, used, 0x20, cellLocals[operand]);
                        used = LoadOperand(code, used, spilled, source, type);
                        used = ManagedValueLocationWasm.WriteSnapshot(code, used);
                    }
                    else if (frameCells[operand] != 0)
                        used = InlineObjectStorageWasm.Set(code, used, frameCells[operand], cellLocals[operand], 0, spilled);
                    else if (backing != 0)
                        used = InlineObjectStorageWasm.Store(code, used, type, backing, operand, spilled, FieldCount(objects, type));
                    else
                    {
                        used = LoadOperand(code, used, spilled, source, type);
                        used = NormalizeSlot(code, used, type);
                        used = Local(code, used, 0x21, operand);
                    }
                }
                else if (kind == WidenReference)
                {
                    used = Local(code, used, 0x20, Spill(spills, maximum, spillFlags, states[state + top], top));
                    int target = records[slot + 3];
                    used = Local(code, used, 0x21, Spill(spills, maximum, spillFlags, target == 0 ? ObjectReference : target, top));
                }
                else if (kind == BoxAggregate || kind == BoxAggregateToInterface || kind == UnboxAggregate ||
                    kind == UnboxAggregateAddress || kind == TestAggregateBox)
                {
                    int box = AggregateBoxing.FindLayout(objects), descriptor = records[slot + 3];
                    int source = Spill(spills, maximum, spillFlags, states[state + top], top);
                    bool address = kind == UnboxAggregateAddress;
                    int resultType = address ? ValueLocation(operand) : kind == UnboxAggregate ? operand :
                        kind == BoxAggregateToInterface ? FieldAccess.Storage(operand) : ObjectReference;
                    int target = Spill(spills, maximum, spillFlags, resultType, top);
                    if (kind == BoxAggregate || kind == BoxAggregateToInterface)
                        used = AggregateBoxing.Box(code, used, source, target, box, descriptor, tracksRoots);
                    else
                    {
                        used = kind == UnboxAggregate || address ? AggregateBoxing.Unbox(code, used, source, operand, box, descriptor, address) :
                            AggregateBoxing.Test(code, used, source, box, descriptor);
                        used = Local(code, used, 0x21, target);
                    }
                }
                else if (kind == BoxValue || kind == UnboxValue || kind == TestBox)
                {
                    int local = Spill(spills, maximum, spillFlags, states[state + top], top);
                    int type = records[slot + 3];
                    if (kind == BoxValue)
                    {
                        int boxStatus = reflection.ObjectTypes.IncludeBox(operand);
                        if (boxStatus < 0) return boxStatus;
                        used = PrimitiveBoxing.Box(code, used, local, operand, type);
                    }
                    else if (kind == UnboxValue) used = PrimitiveBoxing.Unbox(code, used, local, operand, type);
                    else used = PrimitiveBoxing.Test(code, used, local, operand, type);
                    int resultType = kind == UnboxValue ? StackType(PrimitiveBoxing.Storage(operand)) : ObjectReference;
                    used = Local(code, used, 0x21, Spill(spills, maximum, spillFlags, resultType, top));
                }
                else if (kind == CastHandler)
                {
                    int source = states[state + top];
                    if (source != operand && source != NullValue)
                        used = DelegateCastWasm.Emit(code, used, Spill(spills, maximum, spillFlags, source, top),
                            Spill(spills, maximum, spillFlags, operand, top), DelegateCastWasm.Nominal(operand),
                            records[slot + 3] == 0);
                }
                else if (kind == CastClass || kind == TestClass)
                {
                    int type = operand, shape = records[slot + 3], source = states[state + top];
                    int sourceLocal = Spill(spills, maximum, spillFlags, source, top);
                    int targetLocal = Spill(spills, maximum, spillFlags, type, top);
                    if (shape == ReferenceCastWasm.ProvenInterface)
                    {
                        used = LoadOperand(code, used, sourceLocal, source, type);
                        used = Local(code, used, 0x21, targetLocal);
                    }
                    else if (type == PeLibrary.RuntimeTypeMetadata.Assembly)
                        used = RuntimeReflectionWasm.TestAssembly(code, used, frameShape[7], sourceLocal, targetLocal, reflection);
                    else if (type == PeLibrary.RuntimeTypeMetadata.Type)
                        used = RuntimeTypeWasm.Cast(code, used, frameShape[7], sourceLocal, targetLocal,
                            source == PeLibrary.RuntimeTypeMetadata.Type || source == NullValue, kind == CastClass, reflection);
                    else if (shape == ReferenceCastWasm.CheckedInterface)
                        used = ReferenceCastWasm.EmitInterface(code, used, sourceLocal, targetLocal, type,
                            kind == CastClass, frameShape[7], reflection.InterfaceTests);
                    else
                        used = ReferenceCastWasm.Emit(code, used, sourceLocal, targetLocal, type, kind == CastClass);
                }
                else if (kind == Unary || kind == Narrow || kind == ConvertInteger || kind == ConvertFloat)
                {
                    int source = states[state + top];
                    int target = kind == Unary ? (source == NativeZero ? Native : source) : kind == ConvertFloat ? FloatWasm.Target(operand) :
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
                else if (kind == SuppressFinalizer)
                {
                    int source = states[state + top];
                    used = Local(code, used, 0x20, Spill(spills, maximum, spillFlags, source, top));
                    code[used++] = 0x10;
                    used = WasmIndexFixups.Write(code, used, frameShape[7], finalizers.SuppressionFixups);
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
                        if (tracksRoots) used = RootStackWasm.Store(code, used, rootFrame, tracked - 1, temp);
                        if (finalizers != null && finalizers.Registers(type))
                        {
                            // Allocation registers this receiver even if its constructor later throws.
                            used = Local(code, used, 0x20, temp);
                            code[used++] = 0x10;
                            used = WasmIndexFixups.Write(code, used, frameShape[7], finalizers.RegistrationFixups);
                        }
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
                    if (kind == Call && tracked == 0 && catchRegion.Length == 0 && guard < 0 && stackPointer < 0 && first == 0 && returned == result &&
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
                else if (kind == FunctionAddress)
                {
                    used = Const(code, used, operand);
                    used = Local(code, used, 0x21, Spill(spills, maximum, spillFlags, Native, depth));
                }
                else if (kind == NullManagedLocation)
                {
                    int location = records[slot + 3];
                    code[used++] = 0xd0; // ref.null of the byref carrier, not the referenced value.
                    used = WasmBinary.Signed(code, used, WasmBinary.HeapType(location));
                    used = Local(code, used, 0x21, Spill(spills, maximum, spillFlags, location, depth));
                }
                else if (kind == TestNullLocation)
                {
                    int type = states[state + top];
                    int address = Spill(spills, maximum, spillFlags, type, top);
                    used = Local(code, used, 0x20, address);
                    code[used++] = IsNativeInteger(type) ? (byte)0x45 : (byte)0xd1; // i32.eqz or ref.is_null
                    if (IsPacked16ReadLocation(type))
                        used = ManagedLocationWasm.TestCharacterAddress(code, used, address);
                    used = Local(code, used, 0x21, Spill(spills, maximum, spillFlags, I32, top));
                }
                else if (kind == CallPinnedCharacterNative)
                {
                    used = ScopedCharacterPinWasm.Call(code, used,
                        Spill(spills, maximum, spillFlags, CharacterLocation, top - 1),
                        Spill(spills, maximum, spillFlags, Native, top),
                        Spill(spills, maximum, spillFlags, I32, top - 2),
                        Spill(spills, maximum, spillFlags, I32, top - 2), operand,
                        scopedPinLocals, scopedPinLocals + 1, scopedPinLocals + 2, scopedPinLocals + 3,
                        scopedPinLocals + 4, scopedPinLocals + 5, scopedPinLocals + 6, scopedPinLocals + 7,
                        stackPointer, stackBase, stackLimit);
                }
                else if (kind == CallDynamicNative)
                {
                    // CLI: [scalar, data pointer, export token]. The host takes
                    // the token first; scalar and pointer order stays unchanged.
                    used = Local(code, used, 0x20, Spill(spills, maximum, spillFlags, Native, top));
                    used = Local(code, used, 0x20, Spill(spills, maximum, spillFlags, I32, top - 2));
                    used = Local(code, used, 0x20, Spill(spills, maximum, spillFlags, Native, top - 1));
                    code[used++] = 0x10;
                    used = WasmBinary.Unsigned(code, used, operand);
                    used = Local(code, used, 0x21, Spill(spills, maximum, spillFlags, I32, top - 2));
                }
                else if (kind == CallNativeIndirect || kind == CallComCookieLookup)
                {
                    // IL evaluates the token last. The host validates it first,
                    // then receives the original arguments in their IL order.
                    int first = top - (kind == CallComCookieLookup ? 4 : 1);
                    used = Local(code, used, 0x20, Spill(spills, maximum, spillFlags, Native, top));
                    for (int argument = first; argument < top; argument++)
                        used = Local(code, used, 0x20, Spill(spills, maximum, spillFlags, states[state + argument], argument));
                    code[used++] = 0x10;
                    used = WasmBinary.Unsigned(code, used, operand);
                    int returned = records[slot + 3];
                    if (returned != I32) used = Gc(code, used, 0, returned);
                    used = Local(code, used, 0x21, Spill(spills, maximum, spillFlags, returned, first));
                }
                else if (kind == CallIndirect)
                {
                    // [arguments, pointer] -> [result]. The carrier supplies the signature.
                    int arity = arities[operand], shape = offsets[operand];
                    int first = depth - arity - 1;
                    for (int i = 0; i < arity; i++)
                    {
                        int argument = first + i;
                        int source = states[state + argument];
                        used = LoadOperand(code, used, Spill(spills, maximum, spillFlags, source, argument), source, signatures[shape + 1 + i]);
                    }
                    used = Local(code, used, 0x20, Spill(spills, maximum, spillFlags, Native, depth - 1));
                    used = FunctionPointerWasm.CallIndirect(code, used, frameShape[7], pointers, records[slot + 3]);
                    int returned = signatures[shape];
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
                    bool cellBacked = IsValueLocation(frameCells[target]);
                    if (cellBacked) used = ManagedValueLocationWasm.ReadLocation(code, used, cellLocals[target], type);
                    else used = backing == 0 ? Local(code, used, 0x20, target) :
                        InlineObjectStorageWasm.Snapshot(code, used, type, backing, target, FieldCount(objects, type));
                    for (int i = 1; i < arity; i++)
                    {
                        int argument = first + i;
                        int source = states[state + argument];
                        used = LoadOperand(code, used, Spill(spills, maximum, spillFlags, source, argument), source, signatures[shape + 1 + i]);
                    }
                    code[used++] = 0x10;
                    used = WasmBinary.Unsigned(code, used, hostImports + operand);
                    int temporary = backing == 0 && !cellBacked ? target : temporaries + type - FirstClass;
                    used = Local(code, used, 0x21, temporary);
                    if (cellBacked)
                    {
                        used = Local(code, used, 0x20, cellLocals[target]);
                        used = Local(code, used, 0x20, temporary);
                        used = ManagedValueLocationWasm.WriteSnapshot(code, used);
                    }
                    else if (backing != 0)
                        used = InlineObjectStorageWasm.Store(code, used, type, backing, target, temporary, FieldCount(objects, type));
                }
                else if (kind == BitCount)
                {
                    used = Local(code, used, 0x20, Spill(spills, maximum, spillFlags, states[state + top], top));
                    used = BitOperationMembers.Emit(code, used, operand);
                    used = Local(code, used, 0x21, Spill(spills, maximum, spillFlags, I32, top));
                }
                else if (kind == NativeConstant)
                {
                    used = Const(code, used, operand);
                    used = Local(code, used, 0x21, Spill(spills, maximum, spillFlags, Native, depth));
                }
                else if (kind == MultiplyHighUInt64)
                {
                    // [a, b, &low] -> [high]: low = a * b (wrapping) is stored through the location.
                    int a = Spill(spills, maximum, spillFlags, I64, top - 2), b = Spill(spills, maximum, spillFlags, I64, top - 1);
                    int low = Spill(spills, maximum, spillFlags, Int64Location, top);
                    used = Local(code, used, 0x20, a);
                    used = Local(code, used, 0x20, b);
                    code[used++] = 0x7e; // i64.mul
                    used = Local(code, used, 0x21, enumLocals);
                    used = ValueFieldProjectionWasm.Write(code, used, frameShape[7], low, enumLocals, Longs, Int64Location, projections);
                    used = WideMultiplyDivide.High(code, used, a, b);
                    used = Local(code, used, 0x21, a);
                }
                else if (kind == ArrayDataReference)
                {
                    int array = Spill(spills, maximum, spillFlags, states[state + top], top);
                    used = GuardNull(code, used, array);
                    used = Local(code, used, 0x20, array);
                    used = Const(code, used, 0);
                    used = Gc(code, used, 0, ObjectLocation); // struct.new (array, 0)
                    used = Local(code, used, 0x21, Spill(spills, maximum, spillFlags, UnsafeCalls.ElementLocation(operand), top));
                }
                else if (kind == OffsetLocation)
                {
                    int location = Spill(spills, maximum, spillFlags, UnsafeCalls.ElementLocation(operand), top - 1);
                    used = ManagedLocationWasm.LoadField(code, used, location, 0);
                    used = ManagedLocationWasm.LoadField(code, used, location, 1);
                    used = Local(code, used, 0x20, Spill(spills, maximum, spillFlags, states[state + top], top));
                    code[used++] = 0x6a; // i32.add
                    used = Gc(code, used, 0, ObjectLocation);
                    used = Local(code, used, 0x21, location);
                }
                else if (kind == GetRankTwo)
                {
                    int array = Spill(spills, maximum, spillFlags, states[state + top - 2], top - 2);
                    used = MdArrays.EmitGet(code, used, operand, array, Spill(spills, maximum, spillFlags, I32, top - 1),
                        Spill(spills, maximum, spillFlags, I32, top));
                    used = Local(code, used, 0x21, Spill(spills, maximum, spillFlags, operand == Longs ? I64 : I32, top - 2));
                }
                else if (kind == PinArray || kind == PinByteSpan)
                {
                    used = PinnedArraysWasm.Pin(code, used, arrayPins, PinnedArrays.Site(records, slot),
                        Spill(spills, maximum, spillFlags, states[state + top], top),
                        Spill(spills, maximum, spillFlags, Native, top), pinScratch);
                }
                else if (kind == UnpinByteSpan)
                {
                    for (int site = 0; site < arrayPins.Length / 4; site++)
                        if (arrayPins[site * 4] == operand)
                            used = PinnedArraysWasm.Unpin(code, used, arrayPins, site, pinScratch, true);
                }
                else if (kind == NativeSpanFill)
                {
                    int arity = PointerSpanFill.Arity(operand), first = top - arity + 1;
                    used = PointerSpanFill.Emit(code, used, operand, Spill(spills, maximum, spillFlags, states[state + first], first),
                        Spill(spills, maximum, spillFlags, I32, first + 1),
                        arity == 3 ? Spill(spills, maximum, spillFlags, PointerSpanFill.ValueType(operand), top) : -1,
                        scratch + 3, records[slot + 3] - 1);
                }
                else if (kind == ResizeArrayField)
                {
                    int field = operand * Width, aux = records[slot + 3];
                    int vector = ArrayResizeField.Vector(aux);
                    used = ArrayResizeField.Emit(code, used, vector, Spill(spills, maximum, spillFlags, vector, top - 1),
                        Spill(spills, maximum, spillFlags, I32, top), ArrayResizeField.Receiver(aux),
                        FieldAccess.Owner(records[field + 3]), records[field + 1], Spill(spills, maximum, spillFlags, vector, top));
                }
                else if (kind == FillPrimitiveArray)
                {
                    int arity = ArrayFillPrimitive.Arity(operand), first = top - arity + 1;
                    used = ArrayFillPrimitive.Emit(code, used, operand,
                        Spill(spills, maximum, spillFlags, states[state + first], first),
                        Spill(spills, maximum, spillFlags, ArrayFillPrimitive.ValueType(operand), first + 1),
                        arity == 4 ? Spill(spills, maximum, spillFlags, I32, first + 2) : -1,
                        arity == 4 ? Spill(spills, maximum, spillFlags, I32, top) : -1);
                }
                else if (kind == SortPrimitiveArray)
                {
                    bool wide = operand >= 0x0a;
                    used = ArraySortPrimitive.Emit(code, used, operand,
                        Spill(spills, maximum, spillFlags, states[state + top], top),
                        eventLocals, eventLocals + 1, eventLocals + 2, eventLocals + 3, eventLocals + 4,
                        wide ? enumLocals : scratch + 3, wide ? enumLocals + 1 : scratch + 4);
                }
                else if (kind == PrimitiveBitCast)
                {
                    int source = Spill(spills, maximum, spillFlags, UnsafeCalls.StackType(operand), top);
                    used = Local(code, used, 0x20, source);
                    used = UnsafeCalls.Emit(code, used, operand, records[slot + 3]);
                    used = Local(code, used, 0x21, Spill(spills, maximum, spillFlags, UnsafeCalls.StackType(records[slot + 3]), top));
                }
                else if (kind == MultiplyDivideUInt64)
                {
                    int a = Spill(spills, maximum, spillFlags, I64, top - 2);
                    used = WideMultiplyDivide.Emit(code, used, a, Spill(spills, maximum, spillFlags, I64, top - 1),
                        Spill(spills, maximum, spillFlags, I64, top), enumLocals, enumLocals + 1, scratch + 3);
                }
                else if (kind == GarbageCollectorCall)
                {
                    // KeepAlive: nothing to emit; its argument stays in its spill slot and is dropped
                    // with the stack depth. Collect queues every unreachable finalizable object
                    // (Discover); WaitForPendingFinalizers runs them until the queue is empty.
                    if (operand == GarbageCollectorMembers.Collect)
                    {
                        code[used++] = 0x10;
                        used = WasmIndexFixups.Write(code, used, frameShape[7], finalizers.DiscoverFixups);
                        code[used++] = 0x1a; // drop the discovered count
                    }
                    else if (operand == GarbageCollectorMembers.WaitForPendingFinalizers)
                    {
                        code[used++] = 0x03; code[used++] = 0x40; // loop
                        code[used++] = 0x10;
                        used = WasmIndexFixups.Write(code, used, frameShape[7], finalizers.RunNextFixups);
                        code[used++] = 0x0d; code[used++] = 0; // br_if: another finalizer ran
                        code[used++] = 0x0b;
                    }
                }
                else if (kind == NativeMemoryCall)
                {
                    int arity = NativeMemoryMembers.Arity(operand), first = top - arity + 1;
                    var operands = new int[arity];
                    for (int i = 0; i < arity; i++)
                        operands[i] = Spill(spills, maximum, spillFlags, states[state + first + i], first + i);
                    // Six i32 heap locals: the event bank's five counters and one string-scratch i32.
                    var heapScratch = new int[NativeHeapWasm.ScratchCount];
                    for (int i = 0; i < 5; i++) heapScratch[i] = eventLocals + i;
                    heapScratch[5] = scratch + 3;
                    used = NativeMemoryMembers.Emit(code, used, operand, operands,
                        Spill(spills, maximum, spillFlags, Native, first), records[slot + 3], heapScratch);
                }
                else if (kind == FailFast)
                    used = ProcessExit.EmitFailFast(code, used, Spill(spills, maximum, spillFlags, states[state + top], top),
                        Spill(spills, maximum, spillFlags, I32, top), scratch, scratch + 3, scratch + 4, operand);
                else if (kind == ExitProcess)
                    used = ProcessExit.Emit(code, used, Spill(spills, maximum, spillFlags, I32, top), operand);
                else if (kind == ReadTimestamp || kind == ReadFrequency)
                {
                    used = PlatformClock.Emit(code, used, operand, records[slot + 3]);
                    used = Local(code, used, 0x21, Spill(spills, maximum, spillFlags, I64, depth));
                }
                else if (kind == CharacterCall)
                {
                    int value = Spill(spills, maximum, spillFlags, I32, top);
                    used = CharacterMembers.EmitIsWhiteSpace(code, used, value);
                    used = Local(code, used, 0x21, value);
                }
                else if (kind == MathCall)
                {
                    byte type = MathWasm.Type(operand);
                    int first = top - MathWasm.Arity(operand) + 1;
                    used = MathWasm.Emit(code, used, operand, Spill(spills, maximum, spillFlags, type, first),
                        Spill(spills, maximum, spillFlags, type, first + 1 <= top ? first + 1 : top), Spill(spills, maximum, spillFlags, type, top));
                    used = Local(code, used, 0x21, Spill(spills, maximum, spillFlags, type, first));
                }
                else if (kind == ExchangeHandlerField)
                {
                    int owner = FieldAccess.Owner(records[slot + 3]);
                    int receiver = Spill(spills, maximum, spillFlags, states[state + top], top);
                    int oldValue = Spill(spills, maximum, spillFlags, FieldAccess.Storage(records[slot + 3]), top);
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
                    bool backed = operand > 0 && frameCells[operand - 1] == Bytes;
                    if (backed)
                    {
                        used = ManagedLocationWasm.LoadCell(code, used, cellLocals[operand - 1], Bytes, I32);
                        used = Local(code, used, 0x21, operand - 1);
                    }
                    used = MonitorWasm.Emit(code, used, target, operand, frameShape[23]);
                    if (backed)
                        used = InlineObjectStorageWasm.Set(code, used, Bytes, cellLocals[operand - 1], 0, operand - 1);
                }
                else if (kind == Win32Error)
                {
                    bool setter = PeLibrary.MarshalErrorMetadata.IsSetter(operand);
                    if (setter) used = Local(code, used, 0x20, Spill(spills, maximum, spillFlags, I32, top));
                    code[used++] = 0x10;
                    used = WasmBinary.Unsigned(code, used, frameShape[20] + operand - 1);
                    if (!setter) used = Local(code, used, 0x21, Spill(spills, maximum, spillFlags, I32, depth));
                }
                else if ((kind == LoadManagedCell || kind == StoreManagedCell || kind == ClearManagedCell) && operand == 0)
                {
                    int type = records[slot + 3];
                    int position = kind == StoreManagedCell ? top - 1 : top;
                    int address = Spill(spills, maximum, spillFlags, type, position);
                    used = GuardNull(code, used, address);
                    if (kind == LoadManagedCell)
                    {
                        int value = LocatedValue(type);
                        // Location spills use eqref. Restore the exact cell heap at the load.
                        used = LoadOperand(code, used, address, type, type);
                        used = ManagedValueLocationWasm.ReadSnapshot(code, used, value, projections, frameShape[7]);
                        used = Local(code, used, 0x21, Spill(spills, maximum, spillFlags, value, top));
                    }
                    else
                    {
                        used = LoadOperand(code, used, address, type, type);
                        if (kind == StoreManagedCell)
                            used = Local(code, used, 0x20, Spill(spills, maximum, spillFlags, states[state + top], top));
                        else
                        {
                            code[used++] = 0xd0; // ref.null eq
                            code[used++] = 0x6d;
                        }
                        used = ManagedValueLocationWasm.WriteSnapshot(code, used, projections, frameShape[7]);
                    }
                }
                else if (kind == LoadMonoCurrentThread)
                {
                    int target = Spill(spills, maximum, spillFlags, operand, depth);
                    used = CurrentThreadWasm.Load(code, used, operand, target, records[slot + 3],
                        records[slot + Width + 1], records[slot + Width + 2], records[slot + Width + 3], true);
                }
                else if (kind == SetCurrentThreadHandle)
                {
                    int location = records[slot + 3];
                    int type = CellValue(location);
                    int target = temporaries + type - FirstClass;
                    used = CurrentThreadWasm.Load(code, used, type, target, records[slot + Width + 1],
                        records[slot + Width + 2], records[slot + Width + 3], records[slot + 2 * Width + 1]);
                    int address = Spill(spills, maximum, spillFlags, location, top);
                    used = ValueFieldProjectionWasm.Write(code, used, frameShape[7], address, target, operand, location, projections);
                }
                else if (kind == LoadManagedCell || kind == StoreManagedCell || kind == ClearManagedCell)
                {
                    int type = records[slot + 3];
                    int position = kind == StoreManagedCell ? top - 1 : top;
                    int address = Spill(spills, maximum, spillFlags, type, position);
                    if (kind == LoadManagedCell)
                    {
                        used = ValueFieldProjectionWasm.Read(code, used, frameShape[7], address, operand, type, projections);
                        if (IsReferenceLocation(type))
                            used = ManagedLocationWasm.CastCellValue(code, used, Representation(objects, CellValue(type)));
                        used = Local(code, used, 0x21, Spill(spills, maximum, spillFlags, CellValue(type), top));
                    }
                    else
                    {
                        int source = kind == StoreManagedCell ? Spill(spills, maximum, spillFlags, states[state + top], top) : -1;
                        used = ValueFieldProjectionWasm.Write(code, used, frameShape[7], address, source, operand, type, projections);
                    }
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
                else if (kind == PinScopedValue)
                    used = ScopedValuePinWasm.CopyIn(code, used, objects, nativePlans, records[slot + 3],
                        LocatedValue(frame[0]), operand, temporaries, scopedPinLocals + 1,
                        stackPointer, stackBase, stackLimit, spills, maximum, spillFlags, top, projections, frameShape[7]);
                else if (kind == PinStaticBytes)
                {
                    int address = Spill(spills, maximum, spillFlags, Native, depth);
                    int count = records[slot + 3];
                    used = Const(code, used, count);
                    used = Local(code, used, 0x21, address);
                    if (count != 0)
                    {
                        used = StackAllocationWasm.Allocate(code, used, stackPointer, stackBase, stackLimit, address, address, pcLocal);
                        used = Local(code, used, 0x20, address);
                        used = Const(code, used, operand);
                        used = Const(code, used, count);
                        code[used++] = 0xfc; code[used++] = 8; code[used++] = 0; code[used++] = 0; // memory.init 0 0
                    }
                }
                else if (kind == PinString)
                {
                    // The String scratch bank supplies the Char[] and two i32 locals.
                    int source = Spill(spills, maximum, spillFlags, String, top);
                    if (records[slot + 3] != 0) used = GuardNull(code, used, source);
                    used = Utf16MemoryWasm.Pin(code, used, source, scratch, scratch + 3, scratch + 4,
                        Spill(spills, maximum, spillFlags, Native, top), stackPointer, stackBase, stackLimit, pcLocal, pins);
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
                else if (kind == NativeFrameFieldAddress)
                {
                    used = Local(code, used, 0x20, cellLocals[operand]);
                    int offset = records[slot + 3];
                    if (offset != 0)
                    {
                        used = Const(code, used, offset);
                        code[used++] = 0x6a; // i32.add
                    }
                    used = Local(code, used, 0x21, Spill(spills, maximum, spillFlags, Native, depth));
                }
                else if (kind == LoadFrameLocation)
                {
                    int type = records[slot + 3];
                    int target = Spill(spills, maximum, spillFlags, type, depth);
                    if (type == Native || IsValueLocation(type))
                    {
                        used = Local(code, used, 0x20, cellLocals[operand]);
                        used = Local(code, used, 0x21, target);
                    }
                    else used = ManagedLocationWasm.TakeCell(code, used, cellLocals[operand], target, type);
                }
                else if (kind == LoadSingleFrameSpan)
                {
                    int type = records[slot + 3];
                    int target = Spill(spills, maximum, spillFlags, type, depth);
                    used = SpanWasm.CreateSingle(code, used, cellLocals[operand], target, type);
                }
                else if (kind == NewObjectSpan)
                {
                    int vector = Spill(spills, maximum, spillFlags, operand, top - 1);
                    int count = Spill(spills, maximum, spillFlags, I32, top);
                    int resultSpill = Spill(spills, maximum, spillFlags, ObjectSpan, top - 1);
                    used = SpanWasm.Create(code, used, vector, count, resultSpill);
                }
                else if (kind == StringAsSpan || kind == WritableByteArraySpan || kind == ReadOnlyByteArraySpan)
                {
                    int type = kind == StringAsSpan ? CharacterSpan : kind == ReadOnlyByteArraySpan ? ReadOnlyByteSpan : WritableByteSpan;
                    int first = top - operand;
                    int source = Spill(spills, maximum, spillFlags, states[state + first], first);
                    int view = Spill(spills, maximum, spillFlags, type, first);
                    used = SpanWasm.FromOwner(code, used, source, type, kind == StringAsSpan);
                    used = Local(code, used, 0x21, view);
                    if (operand != 0)
                    {
                        // Validate ranges against the original owner's bounds.
                        int start = Spill(spills, maximum, spillFlags, I32, first + 1);
                        int length = operand == 2 ? Spill(spills, maximum, spillFlags, I32, top) : -1;
                        used = SpanWasm.Slice(code, used, type, view, start, length, false);
                        used = Local(code, used, 0x21, view);
                    }
                }
                else if (kind == CharacterArrayAsSpan || kind == ByteArrayAsSpan)
                {
                    int source = Spill(spills, maximum, spillFlags, states[state + top], top);
                    int target = records[slot + 3] - 1;
                    int type = kind == ByteArrayAsSpan ? ReadOnlyByteSpan : WritableCharacterSpan;
                    used = SpanWasm.FromOwner(code, used, source, type, false);
                    used = Local(code, used, 0x21, target >= 0 ? target : Spill(spills, maximum, spillFlags, type, top));
                }
                else if (kind == NativeCharacterSpan)
                {
                    used = NativeCharacterViewWasm.Create(code, used,
                        Spill(spills, maximum, spillFlags, Native, top - 1), Spill(spills, maximum, spillFlags, I32, top));
                    int destination = records[slot + 3];
                    used = Local(code, used, 0x21, destination == 0
                        ? Spill(spills, maximum, spillFlags, WritableCharacterSpan, top - 1) : destination - 1);
                }
                // Read-only conversions only change the logical type.
                // Both descriptors occupy the same Wasm spill bank.
                else if (kind == SpanLength || kind == SpanIsEmpty)
                {
                    int type = records[slot + 3];
                    used = SpanWasm.Length(code, used, Spill(spills, maximum, spillFlags, type, top));
                    if (kind == SpanIsEmpty) code[used++] = 0x45; // i32.eqz
                    used = Local(code, used, 0x21, Spill(spills, maximum, spillFlags, I32, top));
                }
                else if (kind == SpanAddress)
                {
                    int type = records[slot + 3];
                    bool bytes = type == WritableByteSpan;
                    int location = bytes ? WritableByteLocation : CharacterLocation;
                    int view = Spill(spills, maximum, spillFlags, type, top - 1);
                    int index = Spill(spills, maximum, spillFlags, I32, top);
                    if (!bytes && frameShape[25] != 0) used = NativeCharacterViewWasm.ElementLocation(code, used, view, index);
                    else
                    {
                        used = SpanWasm.Address(code, used, bytes ? Bytes : Chars, view, index);
                        used = Gc(code, used, 0, location); // struct.new traced location
                    }
                    used = Local(code, used, 0x21, Spill(spills, maximum, spillFlags, location, top - 1));
                }
                else if (kind == SpanSlice)
                {
                    int type = records[slot + 3];
                    int view = Spill(spills, maximum, spillFlags, type, top - operand);
                    int start = Spill(spills, maximum, spillFlags, I32, top - operand + 1);
                    int length = operand == 2 ? Spill(spills, maximum, spillFlags, I32, top) : -1;
                    used = SpanWasm.Slice(code, used, type, view, start, length, frameShape[25] != 0);
                    used = Local(code, used, 0x21, view);
                }
                else if (kind == EqualCharacterSpans)
                {
                    int type = records[slot + 3];
                    used = SpanWasm.Equal(code, used,
                        Spill(spills, maximum, spillFlags, type, top - 1),
                        Spill(spills, maximum, spillFlags, type, top));
                    if (operand != 0) code[used++] = 0x45; // i32.eqz
                    used = Local(code, used, 0x21, Spill(spills, maximum, spillFlags, I32, top - 1));
                }
                else if (kind == ArrayClear)
                {
                    int first = depth - operand;
                    int vector = states[state + first];
                    int array = Spill(spills, maximum, spillFlags, vector, first);
                    // All slots containing consumed operands are dead after the clear.
                    int cursor = Spill(spills, maximum, spillFlags, I32, operand == 1 ? first : first + 1);
                    int length = operand == 1 ? -1 : Spill(spills, maximum, spillFlags, I32, top);
                    used = ArrayClearWasm.Emit(code, used, objects, vector, array, cursor, length);
                }
                else if (kind == ArrayCopy)
                {
                    int first = depth - operand, target = first + (operand == 3 ? 1 : 2);
                    int sourceType = states[state + first], targetType = states[state + target];
                    int source = Spill(spills, maximum, spillFlags, sourceType, first);
                    int destination = Spill(spills, maximum, spillFlags, targetType, target);
                    int sourceIndex = operand == 3 ? -1 : Spill(spills, maximum, spillFlags, I32, first + 1);
                    int destinationIndex = operand == 3 ? -1 : Spill(spills, maximum, spillFlags, I32, target + 1);
                    int vector = ArrayCopyWasm.Vector(objects, sourceType, targetType, (records[slot + 3] & ArrayCopyElements.Exact) != 0);
                    // The array operands do not use their i32 positions, and the length
                    // does not use its eqref position. These slots are dead after Copy.
                    used = ArrayCopyWasm.Emit(code, used, vector, source, sourceIndex, destination, destinationIndex,
                        Spill(spills, maximum, spillFlags, I32, top), IsValueType(objects, ArrayElement(objects, vector)),
                        (records[slot + 3] & 1) != 0, tracksRoots, Spill(spills, maximum, spillFlags, I32, first),
                        Spill(spills, maximum, spillFlags, I32, target), Spill(spills, maximum, spillFlags, ObjectReference, top));
                }
                else if (kind == SpanRead)
                {
                    int type = records[slot + 3];
                    int view = Spill(spills, maximum, spillFlags, type, top - 1);
                    int index = Spill(spills, maximum, spillFlags, I32, top);
                    used = frameShape[25] != 0 && type == CharacterSpan
                        ? NativeCharacterViewWasm.Read(code, used, view, index)
                        : SpanWasm.Read(code, used, operand, view, index);
                    int resultType = type == ObjectSpan ? ObjectReference : I32;
                    used = Local(code, used, 0x21, Spill(spills, maximum, spillFlags, resultType, top - 1));
                }
                else if (kind == ObjectElementAddress)
                {
                    int array = Spill(spills, maximum, spillFlags, states[state + top - 1], top - 1);
                    int index = Spill(spills, maximum, spillFlags, states[state + top], top);
                    int resultSpill = Spill(spills, maximum, spillFlags, records[slot + 3] >= FirstClass ? ValueLocation(records[slot + 3]) :
                        operand == Ints ? Int32Location : operand == Longs ? Int64Location :
                        operand == Bytes ? WritableByteLocation : ObjectLocation, top - 1);
                    if (records[slot + 3] >= FirstClass)
                    {
                        used = GuardNull(code, used, array);
                        used = GuardArrayIndex(code, used, array, index, false);
                        used = ValueArrayWasm.Take(code, used, operand, array, index, tracksRoots);
                        used = Local(code, used, 0x21, resultSpill);
                    }
                    else used = ManagedLocationWasm.TakeElement(code, used, array, index, resultSpill);
                }
                else if (kind == LoadObjectIndirect)
                {
                    int type = states[state + top];
                    int address = Spill(spills, maximum, spillFlags, type, top);
                    used = ValueFieldProjectionWasm.Read(code, used, frameShape[7], address, operand, type, projections);
                    int value = CellValue(type);
                    used = ManagedLocationWasm.CastCellValue(code, used, Representation(objects, value));
                    used = Local(code, used, 0x21, Spill(spills, maximum, spillFlags, value, top));
                }
                else if (kind == StoreObjectIndirect)
                {
                    int type = states[state + top - 1];
                    int address = Spill(spills, maximum, spillFlags, type, top - 1);
                    int source = Spill(spills, maximum, spillFlags, states[state + top], top);
                    used = ValueFieldProjectionWasm.Write(code, used, frameShape[7], address, source, operand, type, projections);
                }
                else if (kind == NativeFieldPointer)
                {
                    int address = Spill(spills, maximum, spillFlags, Native, top);
                    used = LinearMemoryWasm.GuardNull(code, used, address);
                    if (operand != 0)
                    {
                        used = Local(code, used, 0x20, address);
                        used = Const(code, used, operand);
                        code[used++] = 0x6a;
                        used = Local(code, used, 0x21, address);
                    }
                }
                else if (kind == LoadIndirect)
                {
                    int type = states[state + top];
                    int address = Spill(spills, maximum, spillFlags, type, top);
                    bool packed = IsPacked16ReadLocation(type);
                    if (frameShape[25] != 0 && packed)
                        used = NativeCharacterViewWasm.ReadLocation(code, used, address);
                    else if (packed || type == BooleanLocation || type == StaticByteLocation || type == WritableByteLocation ||
                        type == Int32Location || type == Int64Location || type == NativeIntLocation)
                    {
                        int vector = type == NativeIntLocation ? NativeVector :
                            type == Int32Location ? Ints : type == Int64Location ? Longs : packed ? Chars : Bytes;
                        if (packed) used = GuardNull(code, used, address);
                        used = ValueFieldProjectionWasm.Read(code, used, frameShape[7], address, vector, type, projections);
                    }
                    else
                    {
                        used = LinearMemoryWasm.GuardNull(code, used, address);
                        used = Local(code, used, 0x20, address);
                        used = LinearMemoryWasm.Load(code, used, operand);
                    }
                    if (type == Int16Location) code[used++] = 0xc1; // i32.extend16_s
                    used = Local(code, used, 0x21, Spill(spills, maximum, spillFlags, StackDepths.IndirectType(operand), top));
                }
                else if (kind == StoreIndirect)
                {
                    // CLI order: the address and the value are both evaluated, then the
                    // store raises NullReferenceException for address 0.
                    int type = states[state + top - 1], value = states[state + top];
                    int address = Spill(spills, maximum, spillFlags, type, top - 1);
                    int source = Spill(spills, maximum, spillFlags, value, top);
                    if (frameShape[25] != 0 && IsPacked16Location(type))
                        used = NativeCharacterViewWasm.WriteLocation(code, used, address, source);
                    else if (IsPacked16Location(type) || type == BooleanLocation || type == WritableByteLocation ||
                        type == Int32Location || type == Int64Location || type == NativeIntLocation)
                    {
                        int vector = type == NativeIntLocation ? NativeVector :
                            type == Int32Location ? Ints : type == Int64Location ? Longs : IsPacked16Location(type) ? Chars : Bytes;
                        if (IsPacked16Location(type)) used = GuardNull(code, used, address);
                        used = ValueFieldProjectionWasm.Write(code, used, frameShape[7], address, source, vector, type, projections);
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
                else if (kind == StaticInt32)
                {
                    used = TypeInitializerWasm.Trigger(code, used, fieldInitializers, operand, hostImports);
                    used = StaticInt32Wasm.Emit(code, used, records[slot + 3], operand,
                        Spill(spills, maximum, spillFlags, records[slot + 3] == StaticInt32Wasm.Address ? Int32Location : I32,
                            records[slot + 3] == StaticInt32Wasm.Write ? top : depth));
                }
                else if (kind == StaticReference)
                {
                    used = TypeInitializerWasm.Trigger(code, used, fieldInitializers, operand, hostImports);
                    used = StaticReferenceWasm.Emit(code, used, records, slot, objects,
                        depth == 0 ? NullValue : states[state + top], depth, spills, maximum, spillFlags);
                }
                else if (kind == StaticValue)
                {
                    used = TypeInitializerWasm.Trigger(code, used, fieldInitializers, operand, hostImports);
                    used = StaticValueWasm.Emit(code, used, records, slot,
                        depth == 0 ? NullValue : states[state + top], depth, spills, maximum, spillFlags);
                }
                else if (kind == LoadStaticRvaPointer)
                {
                    used = NativeStaticWasm.LoadData(code, used, staticRegion, staticPages, operand,
                        records[slot + Width + 2], records[slot + Width + 1], records[slot + 3], staticAddress);
                    used = Local(code, used, 0x21, Spill(spills, maximum, spillFlags, Native, depth));
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
                        used = plan == NativeStaticWasm.PointerWordPlan ?
                            LinearMemoryWasm.Load(code, Local(code, used, 0x20, staticAddress), 0x4d) :
                            NativeAggregates.Load(code, used, nativePlans, plan, staticAddress, 0);
                        used = Local(code, used, 0x21, Spill(spills, maximum, spillFlags, type, depth));
                    }
                    else if (plan == NativeStaticWasm.PointerWordPlan)
                    {
                        used = Local(code, used, 0x20, staticAddress);
                        used = Local(code, used, 0x20, Spill(spills, maximum, spillFlags, states[state + top], top));
                        used = LinearMemoryWasm.Store(code, used, 0xdf);
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
                    used = ManagedValueLocationWasm.Take(code, used, FieldAccess.Owner(descriptor), reference, operand, tracksRoots);
                    used = Local(code, used, 0x21, Spill(spills, maximum, spillFlags, ValueLocation(storage), top));
                }
                else if (kind == GetReferenceField || kind == SetReferenceField || kind == TakeReferenceFieldAddress)
                {
                    int position = kind == SetReferenceField ? top - 1 : top;
                    int receiver = Spill(spills, maximum, spillFlags, states[state + position], position);
                    int source = states[state + top];
                    int value = Spill(spills, maximum, spillFlags, source, top);
                    int descriptor = records[slot + 3], vector = records[slot + BranchIl.Width + 2];
                    int resultType = FieldAccess.Storage(descriptor);
                    if (kind == TakeReferenceFieldAddress) resultType = LocationFromIdentity(objects, resultType);
                    int destination = Spill(spills, maximum, spillFlags, resultType, top);
                    used = ReferenceFieldWasm.Emit(code, used, kind, descriptor, operand, vector, receiver, source, value, destination, resultType);
                }
                else if (kind == LoadConstructorAddress)
                {
                    // The verifier owns the address. The i32 placeholder is never dereferenced.
                    used = Const(code, used, 0);
                    used = Local(code, used, 0x21, Spill(spills, maximum, spillFlags, ConstructorAddress, depth));
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
                else if (kind == TakeProjectedFieldAddress)
                {
                    int descriptor = records[slot + 3], owner = FieldAccess.Owner(descriptor);
                    int location = ValueFieldProjections.DecodeLocation(objects, FieldAccess.Storage(descriptor));
                    int source = Spill(spills, maximum, spillFlags, states[state + top], top);
                    used = ValueFieldProjectionWasm.Take(code, used, source, owner, operand, location, IsValueType(objects, owner), tracksRoots);
                    used = Local(code, used, 0x21, Spill(spills, maximum, spillFlags, location, top));
                }
                else if (kind == ClearInteriorField)
                    used = ValueFieldsWasm.ClearAtLocation(code, used, objects, operand, FieldAccess.Owner(records[slot + 3]),
                        Spill(spills, maximum, spillFlags, states[state + top], top), temporaries,
                        states[state + top] == ConstructorAddress, projections, frameShape[7]);
                else if (kind == GetField || kind == SetField)
                {
                    int descriptor = records[slot + 3];
                    int type = FieldAccess.Owner(descriptor);
                    int storage = FieldAccess.Storage(descriptor);
                    int address = kind == GetField ? top : top - 1;
                    int reference = states[state + address] == ConstructorAddress ? 0 :
                        Spill(spills, maximum, spillFlags, states[state + address], address);
                    if (IsValueLocation(states[state + address]) && kind == SetField)
                    {
                        used = GuardNull(code, used, reference);
                        used = ValueFieldsWasm.Update(code, used, objects, type, reference,
                            temporaries + type - FirstClass, operand,
                            Spill(spills, maximum, spillFlags, states[state + top], top), states[state + top], true,
                            projections: projections, start: frameShape[7]);
                    }
                    else if (ManagedValueLocationWasm.UsesCell(objects, type, storage))
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
                                Spill(spills, maximum, spillFlags, source, top), source, storage, tracksRoots);
                        }
                    }
                    else
                    {
                        if (IsValueLocation(states[state + address]))
                        {
                            used = GuardNull(code, used, reference);
                            used = ManagedValueLocationWasm.ReadLocation(code, used, reference, type, projections, frameShape[7]);
                            reference = temporaries + type - FirstClass;
                            used = Local(code, used, 0x21, reference);
                        }
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
                        if (UsesPackedExplicitValue(objects, type))
                            used = ExplicitValueWasm.Load(code, used, objects, type, operand, storage, Spill(spills, maximum, spillFlags, I64, top));
                        else if (HasExplicitSlots(objects, type))
                            used = ExplicitValueWasm.LoadSlot(code, used, objects, type, operand, storage);
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
                    if (frameCells[operand] < 0)
                        used = NativeAggregates.Clear(code, used, nativePlans, -frameCells[operand], cellLocals[operand]);
                    else if (IsValueLocation(frameCells[operand]))
                        used = ManagedValueLocationWasm.ClearFrameCell(code, used, cellLocals[operand]);
                    else if (inlineLocations[operand] != 0)
                        used = InlineObjectStorageWasm.Clear(code, used, inlineLocations[operand], operand, FieldCount(objects, frame[operand]));
                    else if (frame[operand] == PeLibrary.RuntimeTypeMetadata.Handle ||
                        IsObjectReference(frame[operand]) && !IsValueType(objects, frame[operand]))
                    {
                        if (frameCells[operand] != 0)
                        {
                            used = Local(code, used, 0x20, cellLocals[operand]);
                            used = Const(code, used, 0);
                        }
                        code[used++] = 0xd0;
                        code[used++] = StackSpillWasm.NullRef;
                        used = frameCells[operand] != 0 ? Gc(code, used, 0x0e, frameCells[operand]) :
                            Local(code, used, 0x21, operand);
                    }
                    else used = ValueFieldsWasm.Clear(code, used, frame[operand], operand);
                }
                else if (kind == UpdateNestedConstructorField)
                {
                    int source = states[state + top], descriptor = records[slot + 3];
                    used = NestedConstructorFields.Emit(code, used, objects, frame[0], records[slot + Width + 1],
                        FieldAccess.Owner(descriptor), operand, FieldAccess.Storage(descriptor),
                        Spill(spills, maximum, spillFlags, source, top), source, temporaries);
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
                    else if (UsesPackedExplicitValue(objects, type))
                        used = ExplicitValueWasm.Update(code, used, objects, type, local, operand, FieldAccess.Storage(descriptor), replacement, source);
                    else if (HasExplicitSlots(objects, type))
                        used = ExplicitValueWasm.UpdateSlot(code, used, objects, type, local, temporaries + type - FirstClass,
                            operand, FieldAccess.Storage(descriptor), replacement, source);
                    else
                    {
                        bool frameCell = IsValueLocation(frameCells[local]);
                        used = ValueFieldsWasm.Update(code, used, objects, type, frameCell ? cellLocals[local] : local,
                            temporaries + type - FirstClass, operand, replacement, source,
                            frameCell || IsValueLocation(frame[local]), projections: projections, start: frameShape[7]);
                    }
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
                    int element = records[slot + BranchIl.Width + 1];
                    // The exact CLR type belongs to the allocation. Existing typed
                    // array instructions and spills consume its storage supertype.
                    code[used++] = 0xfb;
                    code[used++] = kind == NewArray ? (byte)7 : (byte)9; // default or passive data
                    used = element == 0 ? WasmBinary.Unsigned(code, used, WasmBinary.HeapType(operand)) :
                        primitiveArrays.WriteHeap(code, used, frameShape[7], element);
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
                    int resultType = operand == I32 ? I32 : Native;
                    used = Local(code, used, 0x21, Spill(spills, maximum, spillFlags, resultType, top));
                }
                else if ((kind == ArrayGet || kind == ArraySet) && records[slot + 3] != 0)
                {
                    used = ValueArrayWasm.EmitAccess(code, used, kind == ArrayGet, operand, records[slot + 3], state, top, states,
                        spills, maximum, spillFlags, objects, tracksRoots);
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
                    int value = operand == 0x9a ? ArrayElement(objects, array) : ArrayValueType(array);
                    if (IsEnumArray(objects, states[state + top - 1])) array = states[state + top - 1];
                    used = Gc(code, used, instruction, array);
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
                    if (IsEnumArray(objects, states[state + top - 2])) array = states[state + top - 2];
                    used = Gc(code, used, 0x0e, array);
                }

                if (tracked != 0) used = RootStackWasm.Leave(code, used, rootCount, rootFrame);

                if (kind == Return)
                {
                    // Every live array pin is copied back before the frame returns.
                    if (arrayPins != null)
                        for (int site = 0; site < arrayPins.Length / 4; site++)
                            used = PinnedArraysWasm.Unpin(code, used, arrayPins, site, pinScratch, true);
                    if (scopedValuePin >= 0)
                        used = ScopedValuePinWasm.CopyBack(code, used, nativePlans, records[scopedValuePin + 3],
                            LocatedValue(frame[0]), records[scopedValuePin + 1], projections, frameShape[7]);
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
                    if (pins >= 0) used = PinRegistryWasm.Release(code, used, pins, pinFrame);
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
                else if (kind == ThrowFrameworkArgument)
                {
                    if (operand == PlatformExceptionThrows.Trap) code[used++] = 0x00; // unreachable (decision 0104)
                    else used = ExceptionTagsWasm.Throw(code, used, operand);
                }
                else if (kind == Rethrow)
                    used = ExceptionDispatchWasm.ReraiseCaught(code, used, pcLocal + 2 + operand, objectBase + 1 + operand);
                else
                {
                    if (hasNull || outgoingReferences.Length != 0)
                        used = StackSpillWasm.Transfer(code, used, records, pc, depths, states,
                            outgoingNulls, outgoingReferences, maximum, spills, spillFlags);
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
                    else
                    {
                        int successor = kind == Branch || kind == Leave ? operand : next;
                        used = Const(code, used, successor);
                        // States are tested in pc order. When the constant successor is the
                        // next emitted state, its test follows this one directly, so control
                        // falls through instead of re-entering the dispatch loop. pcLocal is
                        // still written, so exception dispatch observes the same state.
                        fallsThrough = successor == NextEmitted(depths, pc);
                    }
                    used = Local(code, used, 0x21, pcLocal);
                    if (!fallsThrough)
                    {
                        code[used++] = 0x0c;
                        // Catch dispatch adds try_table and one block per tag around the instruction if.
                        code[used++] = (byte)loopDepth;
                    }
                }
                code[used++] = 0x0b;
            }
            if (groupSize != 0) code[used++] = 0x0b; // end the last group
            code[used++] = 0;
            if (catchRegion.Length != 0)
            {
                var fixedObjects = new int[2 * ExceptionDispatchWasm.FixedObjectCount];
                for (int i = 0; i < fixedObjects.Length; i += 2)
                {
                    int function = frameShape[ExceptionDispatchWasm.FrameSlot + i];
                    fixedObjects[i] = function < 0 ? -1 : hostImports + function;
                    fixedObjects[i + 1] = frameShape[ExceptionDispatchWasm.FrameSlot + i + 1];
                }
                used = ExceptionDispatchWasm.End(code, used, pcLocal, catchRegion, exceptionLocals > 1, objectBase,
                    spills, maximum, spillFlags, fixedObjects, filters, tracksRoots ? rootCount : -1, rootFrame);
            }
            code[used++] = 0x0b;
            code[used++] = 0;
            if (tracksRoots || stackPointer >= 0 || arrayPins != null)
            {
                code[used++] = 0x0b; // end try_table
                code[used++] = 0x00;
                code[used++] = 0x0b; // the caught exnref is on the stack
                // An exception leaving the frame: every live array pin is copied back first.
                if (arrayPins != null)
                    for (int site = 0; site < arrayPins.Length / 4; site++)
                        used = PinnedArraysWasm.Unpin(code, used, arrayPins, site, pinScratch, true);
                if (tracksRoots) used = RootStackWasm.Leave(code, used, rootCount, rootFrame);
                if (pins >= 0) used = PinRegistryWasm.Release(code, used, pins, pinFrame);
                if (stackPointer >= 0) used = StackAllocationWasm.Release(code, used, stackPointer, stackLimit, stackFrame);
                code[used++] = 0x0a; // throw_ref
            }
            // Restore the frame before the initializer guard wraps the exception or traps.
            if (guard >= 0) used = TypeInitializerWasm.End(code, used, guard, epoch, activeInitializers,
                tracksRoots ? rootCount : -1, failure);
            code[used++] = 0x0b;
            return used;
        }

        // Bodies with at most this many states keep the flat dispatch chain.
        internal const int MinimumGroupedStates = 32;
        // Bytes BeginGroup and its closing `end` add per group, at most.
        internal const int GroupBytes = 16;

        // About the square root of the state count balances the groups skipped on the way
        // to a state against the states tested inside its group.
        internal static int GroupSize(int states)
        {
            int size = 8;
            while (size * size < states) size++;
            return size;
        }

        // block; br_if 0 (pc >= the first pc of the next group). A pc past every state
        // skips all groups and reaches the loop's trailing unreachable, as before.
        private static int BeginGroup(byte[] code, int used, int[] depths, int pc, int groupSize, int pcLocal)
        {
            int end = pc, counted = 0;
            while (end < depths.Length && counted < groupSize)
            {
                if (depths[end] != 0) counted++;
                end++;
            }
            while (end < depths.Length && depths[end] == 0) end++;
            code[used++] = 0x02;
            code[used++] = 0x40;
            used = Local(code, used, 0x20, pcLocal);
            used = Const(code, used, end);
            code[used++] = 0x4e; // i32.ge_s
            code[used++] = 0x0d;
            code[used++] = 0;
            return used;
        }

        // The next pc the dispatch loop emits a state for, or depths.Length past the last.
        private static int NextEmitted(int[] depths, int pc)
        {
            int candidate = pc + 1;
            while (candidate < depths.Length && depths[candidate] == 0) candidate++;
            return candidate;
        }

        private static int InvokeCharacterSpanCallback(byte[] code, int used, int callback, int span, int state)
        {
            used = GuardNull(code, used, callback);
            used = Local(code, used, 0x20, callback);
            used = Gc(code, used, 2, CharacterSpanCallback);
            code[used++] = 0; // target
            used = Local(code, used, 0x20, span);
            used = Local(code, used, 0x20, state);
            used = Local(code, used, 0x20, callback);
            used = Gc(code, used, 2, CharacterSpanCallback);
            code[used++] = 1; // callable
            used = CastThunk(code, used, WasmBinary.CharacterSpanThunkType);
            code[used++] = 0x14; // call_ref
            return WasmBinary.Unsigned(code, used, WasmBinary.CharacterSpanThunkType);
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
            // Stage 221: general delegate keys share the invocation-list bank (decision 0092).
            type == DelegateReference || PeLibrary.DelegateShapes.IsKey(type) ? EventHandlerReference - 1 :
            type == CharacterSpanCallback ? Delegate - 1 :
            // ReinterpretManagedLocation is verifier-only. Both nominal
            // views keep the same reference in the shared location bank.
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
            code[used++] = StackSpillWasm.NullRef;
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
