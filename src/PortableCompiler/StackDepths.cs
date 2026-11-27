using static PortableCompiler.BranchIl;
using static PeLibrary.ObjectLayouts;
using static PortableCompiler.SignatureTypes;

namespace PortableCompiler
{
    internal static class StackDepths
    {
        internal static int Analyze(int[] records, int[] depths, int[] states, int maximum,
            int[] frame, int result, int[] signatures, int[] offsets, int[] arities, int[] location, int[] catchRegion,
            byte[] outgoingNulls, int[] objects, int[] virtualTypes, byte[] allocatedStates, int[] descriptorMismatch, int[] contexts)
        {
            if (depths.Length == 0)
                return -1;
            var pending = new int[depths.Length];
            var stack = new int[maximum];
            // The proof is separate from the declared stack type. Only methods
            // with deferred virtual calls need an allocation bit per slot.
            var allocated = new byte[allocatedStates.Length == 0 ? 0 : maximum];
            depths[0] = 1;
            // Link pending offsets through their own entries. A refined join can
            // enqueue a processed instruction again, with at most one entry per offset.
            pending[0] = 1;
            int head = 1;
            if (catchRegion.Length != 0)
            {
                for (int clause = 0; clause < catchRegion.Length; clause += ExceptionClauses.Width)
                {
                    int handler = catchRegion[clause + ExceptionClauses.Resume];
                    if (depths[handler] != 0) continue;
                    if (ExceptionClauses.IsBound(catchRegion, clause))
                    {
                        // [caught]: the clause's class, or Object for a catch of System.Object.
                        // MethodCompiler only accepts a bound clause that receives objects.
                        int mask = catchRegion[clause + ExceptionClauses.Mask];
                        if (maximum == 0 || !ManagedExceptionClauses.CatchesObjects(mask)) return -2;
                        depths[handler] = 2;
                        states[handler * maximum] = ManagedExceptionClauses.CaughtClass(mask);
                    }
                    else depths[handler] = 1; // Handler resumes after discarding the exception.
                    pending[handler] = head + 1;
                    head = handler + 1;
                }
            }
            while (head != 0)
            {
                int pc = head - 1;
                head = pending[pc] - 1;
                pending[pc] = 0;
                if (location.Length != 0) location[2] = pc;
                int slot = pc * BranchIl.Width;
                int kind = records[slot];
                int depth = depths[pc] - 1;
                if (depth != 0 && ExceptionClauses.StartsTry(catchRegion, pc)) return -1;
                int operand = records[slot + 1];
                for (int i = 0; i < depth; i++)
                {
                    int index = pc * maximum + i;
                    stack[i] = states[index];
                    if (allocated.Length != 0) allocated[i] = allocatedStates[index];
                }
                if (kind == Return)
                {
                    if (result == Void ? depth != 0 : depth != 1 || !Assignable(stack[0], result, objects))
                        return -1;
                    continue;
                }

                if (kind == ThrowNull)
                {
                    if (depth == 0) return -1;
                    // The null literal raises NullReferenceException. A managed class reference
                    // travels on the object tag after a null test (decision 0054). Interfaces
                    // and value types keep the explicit diagnostic.
                    int thrown = stack[depth - 1];
                    if (thrown != NullValue && (thrown < FirstClass || IsInterface(objects, thrown) ||
                        IsValueType(objects, thrown))) return -2;
                    continue; // throw has no normal successor and clears the evaluation stack.
                }
                if (kind == ThrowFrameworkArgument)
                {
                    if (!Match(stack, depth, I32)) return -1;
                    continue;
                }
                if (kind == Rethrow || kind == EndFinally) continue; // Unwinding drops the evaluation stack.

                bool fieldStore = kind == SetField || kind == UpdateConstructorField;
                int next;
                if (kind == Leave)
                {
                    if (catchRegion.Length == 0) return -2;
                    next = 0; // leave clears the CIL evaluation stack.
                }
                else if (kind == StaticReference)
                {
                    int type = records[slot + Width + 1], action = records[slot + 3];
                    if (action == StaticReferenceWasm.Write)
                        next = depth > 0 && Assignable(stack[depth - 1], type, objects) ? depth - 1 : -2;
                    else
                        next = Push(stack, allocated, depth,
                            action == StaticReferenceWasm.Address ? LocationFromIdentity(objects, type) : type);
                }
                else if (kind == ExchangeHandlerField)
                {
                    if ((depth == 0 || !Assignable(stack[depth - 1], FieldAccess.Owner(records[slot + 3]), objects)) ||
                        frame[records[slot + BranchIl.Width + 1]] != EventHandlerReference ||
                        frame[records[slot + 2 * BranchIl.Width + 1]] != EventHandlerReference)
                        return -1;
                    next = Push(stack, allocated, depth - 1, EventHandlerReference);
                }
                else if ((kind == GetField || fieldStore || kind == FieldAddress) &&
                    records.Length > slot + 2 * BranchIl.Width + 3 && records[slot + 2 * BranchIl.Width + 2] != 0 &&
                    depth >= (fieldStore ? 2 : 1) && IsNativeInteger(stack[depth - (fieldStore ? 2 : 1)]))
                {
                    // Stage 143 (decision 0071): p->F through an S* address in memory 0.
                    int storage = FieldAccess.Storage(records[slot + 3]);
                    if (kind == GetField) next = Push(stack, allocated, depth - 1, StackType(storage));
                    else if (kind == FieldAddress) next = Push(stack, allocated, depth - 1, Native);
                    else next = Assignable(stack[depth - 1], storage, objects) ? depth - 2 : -2;
                }
                else if (kind == VirtualCall)
                {
                    int arity = records[slot + 3];
                    next = CallShape(stack, allocated, depth, virtualTypes, operand, arity, 0, objects);
                    // An unlowered reflection member is valid IL, but a
                    // compiler descriptor cannot be sent to an upstream body.
                    if (next < 0 && depth >= arity && PeLibrary.RuntimeTypeMetadata.IsReference(stack[depth - arity]))
                        return -2;
                    if (next < 0 && depth >= arity)
                    {
                        for (int argument = 1; argument < arity; argument++)
                        {
                            if (stack[depth - arity + argument] != PeLibrary.RuntimeTypeMetadata.Type ||
                                virtualTypes[operand + 1 + argument] < FirstClass) continue;
                            descriptorMismatch[1] = argument;
                            descriptorMismatch[3] = slot + 1;
                            break;
                        }
                    }
                }
                else if (kind == ReadReflection)
                {
                    int receiver = operand == PeLibrary.ReflectionMembers.TypeModule ||
                        operand == PeLibrary.ReflectionMembers.TypeAssembly ||
                        operand == PeLibrary.ReflectionMembers.TypeName ? PeLibrary.RuntimeTypeMetadata.Type : PeLibrary.RuntimeTypeMetadata.Module;
                    int resultType = operand == PeLibrary.ReflectionMembers.TypeModule ? PeLibrary.RuntimeTypeMetadata.Module :
                        operand == PeLibrary.ReflectionMembers.ModuleName ||
                        operand == PeLibrary.ReflectionMembers.TypeName ? String : PeLibrary.RuntimeTypeMetadata.Assembly;
                    if (depth == 0 || !Assignable(stack[depth - 1], receiver, objects)) return -2;
                    next = Push(stack, allocated, depth - 1, resultType);
                }
                else if (kind == Call && records[slot + 3] == 0 &&
                    NativeReceiverCalls.UsesNativeReceiver(contexts[operand], stack, depth - arities[operand]))
                {
                    // Check the unchanged call shape. Binding uses the original
                    // receiver stored in states to select a native body.
                    stack[depth - arities[operand]] = signatures[offsets[operand] + 1];
                    next = CallShape(stack, allocated, depth, signatures, offsets[operand], arities[operand], 0, objects);
                }
                else next = Transition(stack, allocated, depth, kind, operand, records[slot + 3], frame,
                    signatures, offsets, arities, objects);
                if (next < 0)
                {
                    if (kind == EqualRuntimeTypes && depth >= 2)
                    {
                        for (int index = depth - 1; index >= depth - 2; index--)
                        {
                            if (stack[index] < FirstClass) continue;
                            int source = RuntimeTypeReturnDemand.Find(records, pc, index, depths, arities, frame,
                                catchRegion, stack[index], signatures, offsets);
                            if (source < 0) continue;
                            descriptorMismatch[2] = source + 1;
                            break;
                        }
                    }
                    if (kind == Call || kind == NewObject)
                    {
                        int first = kind == NewObject ? 1 : 0;
                        int count = arities[operand], start = depth - count;
                        if (start + first >= 0)
                        {
                            for (int i = first; i < count; i++)
                            {
                                int expected = signatures[offsets[operand] + 1 + i];
                                if (stack[start + i] == PeLibrary.RuntimeTypeMetadata.Type && expected >= FirstClass)
                                {
                                    descriptorMismatch[0] = operand + 1;
                                    descriptorMismatch[1] = i;
                                    break;
                                }
                            }
                        }
                    }
                    return next;
                }
                if (outgoingNulls.Length != 0)
                    for (int i = 0; i < next; i++)
                        outgoingNulls[pc * maximum + i] = stack[i] == NullValue ? (byte)1 :
                            stack[i] == NativeZero ? (byte)2 : (byte)0;
                if (kind == Branch || kind == Conditional || kind == Leave)
                {
                    head = Edge(operand, next, maximum, stack, depths, states, pending, head, objects, allocated, allocatedStates);
                    if (head < 0) return -1;
                }
                if (kind == Switch)
                {
                    int count = records[slot + 3];
                    for (int i = 0; i < count; i++)
                    {
                        head = Edge(SwitchTarget(records, operand, i), next, maximum, stack, depths, states, pending, head, objects, allocated, allocatedStates);
                        if (head < 0) return -1;
                    }
                }
                if (kind != Branch && kind != Leave)
                {
                    head = Edge(records[slot + 2], next, maximum, stack, depths, states, pending, head, objects, allocated, allocatedStates);
                    if (head < 0) return -1;
                }
            }
            return 0;
        }

        private static int CallShape(int[] stack, byte[] allocated, int depth, int[] types, int offset, int arity,
            int constructed, int[] objects)
        {
            int start = depth - arity + constructed;
            if (start < 0) return -1;
            for (int i = constructed; i < arity; i++)
                if (!Assignable(stack[start + i - constructed], types[offset + 1 + i], objects)) return -1;
            int result = types[offset + constructed];
            bool freshClass = constructed != 0 && result >= FirstClass && !IsValueType(objects, result);
            return result == Void ? start : Push(stack, allocated, start, StackType(result), freshClass);
        }

        private static int Transition(int[] stack, byte[] allocated, int depth, int kind, int operand, int extra, int[] frame,
            int[] signatures, int[] offsets, int[] arities, int[] objects)
        {
            if (kind == WidenReference)
                return depth > 0 && Assignable(stack[depth - 1], operand, objects)
                    ? Push(stack, allocated, depth - 1, ObjectReference) : -2;
            if (kind == LoadStaticByteSpan) return Push(stack, allocated, depth, ReadOnlyByteSpan);
            if (kind == TakeStaticByteLocation)
                return Match(stack, depth, ReadOnlyByteSpan) ? Push(stack, allocated, depth - 1, StaticByteLocation) : -2;
            if (kind == TakeCharacterSpanLocation)
                return Match(stack, depth, WritableCharacterSpan) ? Push(stack, allocated, depth - 1, CharacterLocation) : -2;
            if (kind == OffsetStaticByteLocation)
                return depth >= 2 && Match(stack, depth - 1, StaticByteLocation) && IsNativeInteger(stack[depth - 1]) ? depth - 1 : -2;
            if (kind == ReadStaticUInt16)
                return Match(stack, depth, StaticByteLocation) ? Push(stack, allocated, depth - 1, I32) : -2;
            if (kind == LoadString || kind == LoadNativeMessage) return Push(stack, allocated, depth, String);
            if (kind == LoadTypeToken) return Push(stack, allocated, depth, PeLibrary.RuntimeTypeMetadata.Handle);
            // Only an unmanaged pointer (a native int, decision 0060) addresses memory 0.
            // A proven null (NativeZero) spills as Native and raises NullReferenceException
            // at run time, as on the CLR. A managed byref stays a diagnostic.
            if (kind == Monitor)
                return depth > 0 && Assignable(stack[depth - 1], ObjectReference, objects) ? depth - 1 : -2;
            if (kind == Win32Error)
                return PeLibrary.MarshalErrorMetadata.IsSetter(operand)
                    ? Match(stack, depth, I32) ? depth - 1 : -1
                    : Push(stack, allocated, depth, I32);
            if (kind == LoadManagedCell)
                return Match(stack, depth, extra) ? Push(stack, allocated, depth - 1, CellValue(extra)) : -2;
            if (kind == StoreManagedCell)
                return depth >= 2 && Match(stack, depth - 1, extra) &&
                    Assignable(stack[depth - 1], CellValue(extra), objects) ? depth - 2 : -2;
            if (kind == ClearManagedCell)
                return Match(stack, depth, extra) ? depth - 1 : -2;
            if (kind == LoadObject)
                return depth >= 1 && IsNativeInteger(stack[depth - 1]) ? Push(stack, allocated, depth - 1, operand) : -2;
            if (kind == StoreObject)
                return depth >= 2 && IsNativeInteger(stack[depth - 2]) &&
                    (stack[depth - 1] == operand || stack[depth - 1] == NullValue) ? depth - 2 : -2;
            if (kind == InitializeNative)
                return depth >= 1 && IsNativeInteger(stack[depth - 1]) ? depth - 1 : -2;
            if (kind == NativePointerConstructor)
                return depth > 0 && IsNativeInteger(stack[depth - 1]) ? depth : -2;
            if (kind == PinString)
                return Match(stack, depth, String) ? Push(stack, allocated, depth - 1, Native) : -2;
            if (kind == LocalAllocate)
            {
                // ECMA-335 III.3.47: the size is a native unsigned int (an int32 is accepted).
                // A constant zero (ldc.i4.0; conv.u) is a proven NativeZero, which spills as Native.
                if (depth < 1 || (!IsNativeInteger(stack[depth - 1]) && stack[depth - 1] != I32)) return -2;
                return Push(stack, allocated, depth - 1, Native);
            }
            if (kind == LoadFrameLocation) return Push(stack, allocated, depth, extra);
            if (kind == LoadSingleObjectSpan) return Push(stack, allocated, depth, ObjectSpan);
            if (kind == LoadInlineStorage)
                return Push(stack, allocated, depth, extra);
            if (kind == NewObjectSpan)
                return depth >= 2 && stack[depth - 2] == operand && stack[depth - 1] == I32
                    ? Push(stack, allocated, depth - 2, ObjectSpan) : -2;
            if (kind == StringAsSpan)
                return Match(stack, depth, String) ? Push(stack, allocated, depth - 1, CharacterSpan) : -2;
            if (kind == CharacterArrayAsSpan || kind == ByteArrayAsSpan)
            {
                bool bytes = kind == ByteArrayAsSpan;
                if (!Match(stack, depth, bytes ? Bytes : Chars)) return -2;
                return extra == 0 ? Push(stack, allocated, depth - 1, bytes ? ReadOnlyByteSpan : WritableCharacterSpan) : depth - 1;
            }
            if (kind == NativeCharacterSpan)
                return depth >= 2 && IsNativeInteger(stack[depth - 2]) && stack[depth - 1] == I32
                    ? Push(stack, allocated, depth - 2, WritableCharacterSpan) : -2;
            if (kind == SpanAsReadOnly)
                return Match(stack, depth, WritableCharacterSpan) ? Push(stack, allocated, depth - 1, CharacterSpan) : -2;
            if (kind == SpanAddress)
                return Match(stack, depth - 1, WritableCharacterSpan) && Match(stack, depth, I32)
                    ? Push(stack, allocated, depth - 2, CharacterLocation) : -2;
            if (kind == SpanLength || kind == SpanIsEmpty)
                return Match(stack, depth, extra) ? Push(stack, allocated, depth - 1, I32) : -2;
            if (kind == ArrayCopy)
            {
                int start = depth - operand;
                if (start < 0 || !Match(stack, depth, I32)) return -1;
                if (operand == 5 && (stack[start + 1] != I32 || stack[start + 3] != I32)) return -1;
                int vector = ArrayCopyWasm.Vector(objects, stack[start], stack[start + (operand == 3 ? 1 : 2)]);
                return vector == 0 ? -2 : start;
            }
            if (kind == SpanSlice)
                return Match(stack, depth - operand, extra) && Match(stack, depth, I32) &&
                    (operand == 1 || Match(stack, depth - 1, I32))
                    ? Push(stack, allocated, depth - operand - 1, extra) : -2;
            if (kind == SpanRead)
                return Match(stack, depth - 1, extra) && Match(stack, depth, I32)
                    ? Push(stack, allocated, depth - 2, extra == ObjectSpan ? ObjectReference : I32) : -2;
            if (kind == ObjectElementAddress)
            {
                if (depth < 2 || !ArrayIndex(stack, depth)) return -1;
                int array = stack[depth - 2];
                if (array != operand && array != NullValue) return -2;
                int location = extra >= FirstClass ? ValueLocation(extra) : operand == Ints ? Int32Location : ObjectLocation;
                return Push(stack, allocated, depth - 2, location);
            }
            if (kind == LoadObjectIndirect)
            {
                if (depth < 1 || !IsReferenceLocation(stack[depth - 1])) return -2;
                return Push(stack, allocated, depth - 1, CellValue(stack[depth - 1]));
            }
            if (kind == StoreObjectIndirect)
            {
                if (depth < 2) return -2;
                int type = stack[depth - 2];
                return IsReferenceLocation(type) && Assignable(stack[depth - 1], CellValue(type), objects) ? depth - 2 : -2;
            }
            if (kind == LoadIndirect)
            {
                if (depth < 1) return -2;
                int address = stack[depth - 1];
                if (!IsNativeInteger(address) && (address != CharacterLocation || operand != 0x49) &&
                    (address != BooleanLocation && address != StaticByteLocation || operand != 0x47) &&
                    (address != Int32Location || operand != 0x4a && operand != 0x4b)) return -2;
                return Push(stack, allocated, depth - 1, IndirectType(operand));
            }
            if (kind == StoreIndirect)
            {
                if (depth < 2 || !IndirectValue(operand, stack[depth - 1])) return -2;
                int address = stack[depth - 2];
                if (!IsNativeInteger(address) && (address != CharacterLocation || operand != 0x53) &&
                    (address != BooleanLocation || operand != 0x52) && (address != Int32Location || operand != 0x54)) return -2;
                return depth - 2;
            }
            if (kind == GetObjectType)
            {
                if (depth == 0) return -1;
                int receiver = stack[depth - 1];
                if (receiver != NullValue && receiver != ObjectReference && receiver != String &&
                    (receiver < FirstClass || receiver >= ConstructorAddress ||
                    IsValueType(objects, receiver))) return -2;
                return Push(stack, allocated, depth - 1, PeLibrary.RuntimeTypeMetadata.Type);
            }
            if (kind == EqualRuntimeTypes)
                return Match(stack, depth, PeLibrary.RuntimeTypeMetadata.Type) &&
                    Match(stack, depth - 1, PeLibrary.RuntimeTypeMetadata.Type) ? Push(stack, allocated, depth - 2, I32) : -2;
            if (kind == TypeFromHandle)
                return depth > 0 && stack[depth - 1] == PeLibrary.RuntimeTypeMetadata.Handle
                    ? Push(stack, allocated, depth - 1, PeLibrary.RuntimeTypeMetadata.Type) : -1;
            // [] -> [writer]; [writer, string-or-null] -> []; [writer] -> [] for WriteLine().
            if (kind == ConsoleWriter) return Push(stack, allocated, depth, PeLibrary.ConsoleStorage.Writer);
            if (kind == ConsoleReadLine) return Push(stack, allocated, depth, String);
            // Stage 156 (decision 0083): [] -> [i32] for the presence check; helpers leave the
            // stack unchanged; CallNative has the shape of the thunk's own method (operand).
            if (kind == NativePresent) return Push(stack, allocated, depth, I32);
            if (kind == NativeHelper) return depth;
            if (kind == CallNative)
                return CallShape(stack, allocated, depth, signatures, offsets[operand], arities[operand], 0, objects);
            if (kind == ConsoleWrite)
            {
                int writer = operand == PeLibrary.ConsoleMetadata.WriteEmptyLine ? depth - 1 : depth - 2;
                if (writer < 0 || stack[writer] != PeLibrary.ConsoleStorage.Writer) return -1;
                return writer == depth - 1 || Match(stack, depth, String) ? writer : -1;
            }
            if (kind == ClearValue || kind == ResourceLookup) return depth;
            if (kind == NullLiteral) return Push(stack, allocated, depth, NullValue);
            if (kind == LoadEventArgsEmpty || kind == NewEventArgs) return Push(stack, allocated, depth, EventArgsReference);
            if (kind == NewRootObject) return Push(stack, allocated, depth, ObjectReference, true);
            if (kind == CombineHandler || kind == RemoveHandler)
                return depth >= 2 && Assignable(stack[depth - 1], DelegateReference, objects) &&
                    Assignable(stack[depth - 2], DelegateReference, objects)
                    ? Push(stack, allocated, depth - 2, EventHandlerReference) : -2;
            if (kind == CastHandler)
            {
                if (depth == 0 || !Assignable(stack[depth - 1], DelegateReference, objects)) return -2;
                // The CLR types differ, but every supported Delegate uses the EventHandler heap.
                if (stack[depth - 1] != NullValue) stack[depth - 1] = EventHandlerReference;
                return depth;
            }
            if (kind == BoxValue || kind == UnboxValue || kind == TestBox)
            {
                if (depth < 1) return -1;
                int source = stack[depth - 1], storage = PrimitiveBoxing.Storage(operand);
                if (kind == BoxValue)
                    return Assignable(source, storage, objects)
                        ? Push(stack, allocated, depth - 1, ObjectReference) : -1;
                if (source != NullValue && !IsReference(source) || IsValueType(objects, source)) return -1;
                return Push(stack, allocated, depth - 1, kind == UnboxValue ? StackType(storage) : ObjectReference);
            }
            if (kind == CastClass || kind == TestClass)
            {
                if (depth < 1 || IsValueType(objects, stack[depth - 1]) ||
                    (stack[depth - 1] != NullValue && !IsReference(stack[depth - 1]))) return -1;
                return Push(stack, allocated, depth - 1, operand);
            }
            if (kind == StringEquals)
                return Match(stack, depth, String) && Match(stack, depth - 1, String)
                    ? Push(stack, allocated, depth - 2, I32) : -1;
            if (kind == JoinStrings)
                return Match(stack, depth, String) && Match(stack, depth - 1, String)
                    ? Push(stack, allocated, depth - 2, String) : -1;
            if (kind == StringFromChars)
                return Match(stack, depth, Chars) ? Push(stack, allocated, depth - 1, String) : -1;
            if (kind == StringFromSpan)
                return Match(stack, depth, CharacterSpan) ? Push(stack, allocated, depth - 1, String) : -1;
            if (kind == AllocateString)
                return Match(stack, depth, Native) ? Push(stack, allocated, depth - 1, String) : -1;
            if (kind == StringConstructionSpan)
                return Match(stack, depth, I32) && Match(stack, depth - 1, String)
                    ? Push(stack, allocated, depth - 2, WritableCharacterSpan) : -1;
            if (kind == StringRange)
                return Match(stack, depth - 2, String) && Match(stack, depth - 1, I32) && Match(stack, depth, I32)
                    ? Push(stack, allocated, depth - 3, String) : -2;
            if (kind == StringLength || kind == StringFirstCodeUnit)
                return Match(stack, depth, String) ? Push(stack, allocated, depth - 1, I32) : -1;
            if (kind == StringCharacter || kind == StringContainsCodeUnit)
                return Match(stack, depth, I32) && Match(stack, depth - 1, String)
                    ? Push(stack, allocated, depth - 2, I32) : -1;
            if (kind == LoadFunction) return Push(stack, allocated, depth, MethodPointer);
            // Decision 0078: a function pointer is a native int; calli takes it last.
            if (kind == FunctionAddress) return Push(stack, allocated, depth, Native);
            if (kind == CallIndirect)
            {
                int arity = arities[operand], shape = offsets[operand];
                if (depth < arity + 1 || !IsNativeInteger(stack[depth - 1])) return -2;
                int start = depth - 1 - arity;
                for (int i = 0; i < arity; i++)
                    if (!Assignable(stack[start + i], signatures[shape + 1 + i], objects)) return -1;
                int returned = signatures[shape];
                return returned == Void ? start : Push(stack, allocated, start, StackType(returned));
            }
            if (kind == NewDelegate)
            {
                if (!Match(stack, depth, MethodPointer) || depth < 2 || stack[depth - 2] != extra) return -1;
                return Push(stack, allocated, depth - 2, operand);
            }
            if (kind == InvokeDelegate)
            {
                if (operand == CharacterSpanCallback)
                    return depth >= 3 && IsNativeInteger(stack[depth - 1]) &&
                        Match(stack, depth - 1, WritableCharacterSpan) && Match(stack, depth - 2, CharacterSpanCallback)
                        ? depth - 3 : -2;
                if (operand == EventHandlerReference)
                    return Match(stack, depth, EventArgsReference) && depth >= 3 &&
                        Assignable(stack[depth - 2], ObjectReference, objects) && Match(stack, depth - 2, EventHandlerReference)
                        ? depth - 3 : -1;
                if (!Match(stack, depth, Delegate)) return -1;
                return Push(stack, allocated, depth - 1, I32);
            }
            if (kind == Load) return Push(stack, allocated, depth, StackType(frame[operand]));
            if (kind == Constant || kind == WideConstant)
                return Push(stack, allocated, depth, kind == WideConstant ? I64 : I32);
            if (kind == SingleConstant || kind == DoubleConstant)
                return Push(stack, allocated, depth, kind == SingleConstant ? F32 : F64);
            if (kind == Store) return depth > 0 && Assignable(stack[depth - 1], frame[operand], objects) ? depth - 1 : -1;
            if (kind == CheckedConversion)
            {
                if (depth < 1 || !IsScalar(stack[depth - 1])) return -1;
                stack[depth - 1] = CheckedConversionWasm.Result(operand);
                return depth;
            }
            if (kind == Unary || kind == Narrow || kind == ConvertInteger || kind == ConvertFloat)
            {
                if (depth < 1) return -1;
                int source = stack[depth - 1];
                if (!IsScalar(source)) return -1;
                if (IsNativeInteger(source) && kind != ConvertInteger && kind != Narrow) return -2;
                if (kind == Unary)
                    return IsFloat(source) && operand != 0x6c ? -1 : depth;
                if (kind == ConvertFloat)
                {
                    if (IsFloat(source) && (operand == 0x76 || operand == UnsignedSingle)) return -1;
                    stack[depth - 1] = FloatWasm.Target(operand);
                }
                else
                {
                    if (IsFloat(source) && (operand == 0xd3 || operand == 0xe0)) return -2;
                    stack[depth - 1] = kind == ConvertInteger ?
                        extra == ProvenNativeZero ? NativeZero : IntegerConversionType(operand) : I32;
                }
                return depth;
            }
            if (kind == Dup)
                return depth < 1 ? -1 : Push(stack, allocated, depth, stack[depth - 1],
                    allocated.Length != 0 && allocated[depth - 1] != 0);
            if (kind == Pop) return depth - 1;
            if (kind == Switch) return Match(stack, depth, I32) ? depth - 1 : -1;
            if (kind == CheckedInteger)
            {
                if (depth < 2) return -1;
                int type = stack[depth - 1];
                if (IsNativeInteger(type) || IsNativeInteger(stack[depth - 2]))
                {
                    // Checked native arithmetic (decision 0065) uses the 4-byte wasm32 width,
                    // as on the 32-bit CLR. C# scales stackalloc sizes with mul.ovf.un.
                    if ((!IsNativeInteger(type) && type != I32) ||
                        (!IsNativeInteger(stack[depth - 2]) && stack[depth - 2] != I32)) return -2;
                    stack[depth - 2] = Native;
                    return depth - 1;
                }
                return IsInteger(type) && Match(stack, depth - 1, type) ? depth - 1 : -1;
            }
            if (kind == Binary || kind == Conditional)
            {
                int needed = kind == Binary || (extra != 0 && extra != 0x45) ? 2 : 1;
                if (depth < needed) return -1;
                int type = stack[depth - needed];
                int top = stack[depth - 1];
                if (IsValueType(objects, type) || IsValueType(objects, top) || IsValueLocation(type) || IsValueLocation(top)) return -2;
                if (type == NullPointer || top == NullPointer)
                {
                    if (needed == 1) return depth - 1;
                    if ((type != NullPointer && type != NativeZero) || (top != NullPointer && top != NativeZero))
                        return -2;
                    if (kind == Conditional) return extra == 0x46 || extra == 0x47 ? depth - needed : -2;
                    return operand == 0x46 || operand == 0x4b && top == NativeZero
                        ? Push(stack, allocated, depth - 2, I32) : -2;
                }
                if (IsNativeInteger(type) || IsNativeInteger(top))
                {
                    // CLI comparisons can mix native int and int32. Both
                    // use i32 bits here, including the common ldlen test.
                    if ((!IsNativeInteger(type) && type != I32) || (!IsNativeInteger(top) && top != I32)) return -2;
                    if (kind != Binary) return depth - needed;
                    if (IsComparison(operand)) return Push(stack, allocated, depth - 2, I32);
                    // ECMA-335 Table III.2: native int with int32 or native int yields native
                    // int for arithmetic and bitwise operations, and a shift keeps the type of
                    // its value. Pointer arithmetic (decision 0060) uses this. Both are i32 on
                    // wasm32, so the emitted i32 operation is unchanged.
                    if (operand < 0x6a || operand > 0x76) return -2;
                    if (IsShift(operand) && !IsNativeInteger(type)) return Push(stack, allocated, depth - 2, I32);
                    return Push(stack, allocated, depth - 2, Native);
                }
                if (type == NullValue || top == NullValue || IsReference(type) || IsReference(top))
                {
                    if (needed == 1) return depth - 1;
                    if (!ReferenceComparison(type, top, objects)) return -1;
                    if (kind == Conditional)
                        return extra == 0x46 || extra == 0x47 ? depth - needed : -1;
                    // C# materializes a boolean type test with isinst, ldnull,
                    // cgt.un. Only accept this reference ordering against null.
                    return operand == 0x46 || (operand == 0x4b && top == NullValue)
                        ? Push(stack, allocated, depth - 2, I32) : -1;
                }
                if (IsFloat(type) || IsFloat(top))
                {
                    if (needed != 2 || !IsFloat(type) || !IsFloat(top)) return -1;
                    if (kind == Conditional) return depth - 2;
                    if (!IsComparison(operand) && operand != 0x6f && (operand < 0x6a || operand > 0x6d)) return -1;
                    return Push(stack, allocated, depth - 2, IsComparison(operand) ? I32 : FloatWidth(type, top));
                }
                if (!IsInteger(type)) return -1;
                if (needed == 2 && !Match(stack, depth, kind == Binary && IsShift(operand) ? I32 : type))
                    return -1;
                int remaining = depth - needed;
                return kind == Binary ? Push(stack, allocated, remaining, IsComparison(operand) ? I32 : type) : remaining;
            }
            if (kind == MathCall)
            {
                // Every mapped overload is T(T) or T(T, T) over one numeric stack type.
                byte type = MathWasm.Type(operand);
                int arity = MathWasm.Arity(operand);
                if (!Match(stack, depth, type) || (arity == 2 && !Match(stack, depth - 1, type))) return -1;
                return Push(stack, allocated, depth - arity, type);
            }
            if (kind == Call || kind == NewObject)
            {
                int count = arities[operand];
                int protectedReceiver = kind == Call ? extra >> 1 : 0;
                if (protectedReceiver != 0 && (depth < count ||
                    !Assignable(stack[depth - count], protectedReceiver, objects))) return -2;
                return CallShape(stack, allocated, depth, signatures, offsets[operand], count, kind == NewObject ? 1 : 0, objects);
            }
            if (kind == ConstructValue)
            {
                // The receiver entry is the addressed location's value, pushed by
                // the lowered ldloca or ldarg.0. The callee returns the replacement,
                // which the emitter stores back to that location.
                int count = arities[operand], offset = offsets[operand];
                int start = depth - count;
                if (start < 0 || frame[extra] != signatures[offset + 1] ||
                    !Assignable(stack[start], signatures[offset + 1], objects)) return -1;
                for (int i = 1; i < count; i++)
                    if (!Assignable(stack[start + i], signatures[offset + 1 + i], objects))
                        return -1;
                return start;
            }
            if (kind == SuppressFinalizer)
                return depth != 0 && Assignable(stack[depth - 1], ObjectReference, objects) ? depth - 1 : -2;
            if (kind == BaseCtor)
                return depth > 0 && stack[depth - 1] >= FirstClass && !IsInterface(objects, stack[depth - 1]) &&
                    !IsValueType(objects, stack[depth - 1]) ? depth - 1 : -1;
            if (kind == StaticFieldAddress) return Push(stack, allocated, depth, Native);
            if (kind == StaticInt32)
                return extra == StaticInt32Wasm.Write ? Match(stack, depth, I32) ? depth - 1 : -1 :
                    Push(stack, allocated, depth, extra == StaticInt32Wasm.Address ? Int32Location : I32);
            if (kind == LoadStaticField || kind == LoadNativeStatic) return Push(stack, allocated, depth, StackType(extra));
            if (kind == StoreStaticField || kind == StoreNativeStatic)
                return depth > 0 && Assignable(stack[depth - 1], extra, objects) ? depth - 1 : -1;
            if (kind == TakeValueFieldAddress)
            {
                if (depth == 0 || !Assignable(stack[depth - 1], FieldAccess.Owner(extra), objects)) return -1;
                return Push(stack, allocated, depth - 1, ValueLocation(FieldAccess.Storage(extra)));
            }
            if (kind == TakeReferenceFieldAddress)
            {
                if (depth == 0 || !Assignable(stack[depth - 1], FieldAccess.Owner(extra), objects)) return -1;
                return Push(stack, allocated, depth - 1, LocationFromIdentity(objects, FieldAccess.Storage(extra)));
            }
            if (kind == LoadConstructorAddress)
                return Push(stack, allocated, depth, ConstructorAddress);
            if (kind == GetField || kind == GetReferenceField)
            {
                if (depth == 0) return -1;
                int receiver = stack[depth - 1];
                if (receiver == ConstructorAddress) receiver = frame[0];
                if (IsValueLocation(receiver) && !IsExplicitValue(objects, LocatedValue(receiver)))
                    receiver = LocatedValue(receiver);
                if (!Assignable(receiver, FieldAccess.Owner(extra), objects)) return -1;
                return Push(stack, allocated, depth - 1, StackType(FieldAccess.Storage(extra)));
            }
            if (kind == ClearInteriorField)
            {
                int owner = FieldAccess.Owner(extra);
                return depth != 0 && (stack[depth - 1] == ValueLocation(owner) ||
                    stack[depth - 1] == ConstructorAddress && frame[0] == owner) ? depth - 1 : -2;
            }
            if (kind == SetField || kind == SetReferenceField)
            {
                if (depth < 2 || !Assignable(stack[depth - 1], FieldAccess.Storage(extra), objects)) return -1;
                int owner = FieldAccess.Owner(extra), receiver = stack[depth - 2];
                // Aggregate stores replace the snapshot at an actual managed
                // location. A value on the stack is only a copy.
                bool valid = IsValueType(objects, owner)
                    ? !IsExplicitValue(objects, owner) && receiver == ValueLocation(owner)
                    : Assignable(receiver, owner, objects);
                return valid ? depth - 2 : -1;
            }
            if (kind == UpdateConstructorField)
                return depth >= 2 && stack[depth - 2] == ConstructorAddress &&
                    frame[0] == FieldAccess.Owner(extra) && Assignable(stack[depth - 1], FieldAccess.Storage(extra), objects) ? depth - 2 : -2;
            if (kind == UpdateValue)
                return depth > 0 && Assignable(stack[depth - 1], FieldAccess.Storage(extra), objects) ? depth - 1 : -1;
            if (kind == NewArray || kind == InitializedArray)
            {
                if (!ArrayIndex(stack, depth)) return -1;
                stack[depth - 1] = operand;
            }
            else if (kind == ArrayLength)
            {
                if (depth < 1 || (stack[depth - 1] != NullValue && !IsArray(stack[depth - 1]) &&
                    ArrayElement(objects, stack[depth - 1]) == 0)) return -1;
                stack[depth - 1] = Native;
            }
            else if (kind == ArrayGet)
            {
                if (extra != 0)
                    return depth >= 2 && ArrayIndex(stack, depth) && Match(stack, depth - 1, operand)
                        ? Push(stack, allocated, depth - 2, extra) : -1;
                if (operand == 0x9a)
                {
                    if (depth < 2 || !ArrayIndex(stack, depth)) return -1;
                    int arrayType = stack[depth - 2], element = ArrayElement(objects, arrayType);
                    if (element == 0 || IsValueType(objects, element) || IsEnumArray(objects, arrayType)) return -2;
                    return Push(stack, allocated, depth - 2, element);
                }
                byte array = ArrayLoadType(operand);
                if (depth < 2 || !ArrayIndex(stack, depth) ||
                    (!Match(stack, depth - 1, array) && EnumArrayStorage(objects, stack[depth - 2]) != array)) return -1;
                stack[depth - 2] = ArrayValueType(array);
                return depth - 1;
            }
            else if (kind == ArraySet)
            {
                if (extra != 0)
                    return depth >= 3 && ArrayIndex(stack, depth - 1) && Match(stack, depth - 2, operand) &&
                        Assignable(stack[depth - 1], extra, objects) ? depth - 3 : -1;
                if (operand == 0xa2)
                {
                    if (depth < 3 || !ArrayIndex(stack, depth - 1)) return -1;
                    int arrayType = stack[depth - 3], element = ArrayElement(objects, arrayType);
                    if (element == 0 || IsValueType(objects, element) || IsEnumArray(objects, arrayType)) return -2;
                    return Assignable(stack[depth - 1], element, objects) ? depth - 3 : -1;
                }
                byte array = ArrayStoreType(operand);
                if (depth < 3 || !Assignable(stack[depth - 1], ArrayValueType(array), objects) ||
                    !ArrayIndex(stack, depth - 1) ||
                    (!Match(stack, depth - 2, array) && EnumArrayStorage(objects, stack[depth - 3]) != array))
                    return -1;
                return depth - 3;
            }
            return depth;
        }

        // The CLI stack type of ldind.*: small integers widen to int32.
        internal static int IndirectType(int opcode) =>
            opcode == 0x4c ? I64 : opcode == 0x4d ? Native : opcode == 0x4e ? F32 : opcode == 0x4f ? F64 : I32;

        // stind.* stores the low bits of an int32 for i1/i2/i4, a native int for
        // stind.i (int32 converts implicitly on wasm32), and either float width for r4/r8.
        private static bool IndirectValue(int opcode, int value) =>
            opcode == 0x55 ? value == I64 :
            opcode == 0x56 || opcode == 0x57 ? value == F32 || value == F64 :
            opcode == 0xdf ? IsNativeInteger(value) || value == I32 :
            value == I32;

        private static bool Match(int[] stack, int depth, int type) =>
            depth > 0 && (stack[depth - 1] == type || stack[depth - 1] == NullValue && IsReference(type));

        private static bool ArrayIndex(int[] stack, int depth) =>
            depth > 0 && (stack[depth - 1] == I32 || IsNativeInteger(stack[depth - 1]));

        private static int Push(int[] stack, byte[] allocated, int depth, int type, bool fresh = false)
        {
            if (depth >= stack.Length) return -1;
            stack[depth] = type;
            if (allocated.Length != 0) allocated[depth] = fresh ? (byte)1 : (byte)0;
            return depth + 1;
        }

        private static int Edge(int pc, int count, int maximum, int[] stack, int[] depths,
            int[] states, int[] pending, int head, int[] objects, byte[] allocated, byte[] allocatedStates)
        {
            if (pc >= depths.Length) return -1;
            int offset = pc * maximum;
            bool changed = depths[pc] == 0;
            if (changed)
            {
                depths[pc] = count + 1;
                for (int i = 0; i < count; i++)
                {
                    states[offset + i] = stack[i];
                    if (allocated.Length != 0) allocatedStates[offset + i] = allocated[i];
                }
            }
            else
            {
                if (depths[pc] != count + 1) return -1;
                for (int i = 0; i < count; i++)
                {
                    int current = states[offset + i], incoming = stack[i];
                    // The bit survives only if every incoming path proves the
                    // same class. A lost proof requeues downstream consumers.
                    if (allocated.Length != 0 && allocatedStates[offset + i] != 0 &&
                        (allocated[i] == 0 || current != incoming))
                    {
                        allocatedStates[offset + i] = 0;
                        changed = true;
                    }
                    if (current != incoming && (IsValueType(objects, current) || IsValueType(objects, incoming))) return -1;
                    if (current == incoming || incoming == NullValue && IsReference(current) ||
                        incoming == NativeZero && (current == Native || current == NullPointer)) continue;
                    if ((current != NullValue || !IsReference(incoming)) &&
                        (current != NativeZero || (incoming != Native && incoming != NullPointer))) return -1;
                    states[offset + i] = incoming;
                    changed = true;
                }
            }
            if (!changed || pending[pc] != 0) return head;
            pending[pc] = head + 1;
            return pc + 1;
        }
    }
}
