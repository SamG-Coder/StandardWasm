using static PortableCompiler.BranchIl;
using static PeLibrary.ObjectLayouts;
using static PortableCompiler.SignatureTypes;

namespace PortableCompiler
{
    internal static class StackDepths
    {
        internal static int Analyze(int[] records, int[] depths, int[] states, int maximum,
            int[] frame, int result, int[] signatures, int[] offsets, int[] arities, int[] location, int[] catchRegion,
            byte[] outgoingNulls, int[] objects, int[] virtualTypes, byte[] allocatedStates)
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
                    depths[handler] = 1; // Handler resumes after discarding the exception.
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
                if (kind == Rethrow || kind == EndFinally) continue; // Unwinding drops the evaluation stack.

                int next;
                if (kind == Leave)
                {
                    if (catchRegion.Length == 0) return -2;
                    next = 0; // leave clears the CIL evaluation stack.
                }
                else if (kind == ExchangeHandlerField)
                {
                    if ((depth == 0 || !Assignable(stack[depth - 1], FieldAccess.Owner(records[slot + 3]), objects)) ||
                        frame[records[slot + BranchIl.Width + 1]] != EventHandlerReference ||
                        frame[records[slot + 2 * BranchIl.Width + 1]] != EventHandlerReference)
                        return -1;
                    next = Push(stack, allocated, depth - 1, EventHandlerReference);
                }
                else if ((kind == GetField || kind == SetField || kind == FieldAddress) &&
                    records.Length > slot + 2 * BranchIl.Width + 3 && records[slot + 2 * BranchIl.Width + 2] != 0 &&
                    depth >= (kind == SetField ? 2 : 1) && IsNativeInteger(stack[depth - (kind == SetField ? 2 : 1)]))
                {
                    // Stage 143 (decision 0071): p->F through an S* address in memory 0.
                    int storage = FieldAccess.Storage(records[slot + 3]);
                    if (kind == GetField) next = Push(stack, allocated, depth - 1, StackType(storage));
                    else if (kind == FieldAddress) next = Push(stack, allocated, depth - 1, Native);
                    else next = Assignable(stack[depth - 1], storage, objects) ? depth - 2 : -2;
                }
                else if (kind == VirtualCall)
                    next = CallShape(stack, allocated, depth, virtualTypes, operand, records[slot + 3], 0, objects);
                else next = Transition(stack, allocated, depth, kind, operand, records[slot + 3], frame,
                    signatures, offsets, arities, objects);
                if (next < 0)
                    return next;
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
            if (kind == LoadString) return Push(stack, allocated, depth, String);
            if (kind == LoadTypeToken) return Push(stack, allocated, depth, PeLibrary.RuntimeTypeMetadata.Handle);
            // Only an unmanaged pointer (a native int, decision 0060) addresses memory 0.
            // A proven null (NativeZero) spills as Native and raises NullReferenceException
            // at run time, as on the CLR. A managed byref stays a diagnostic.
            if (kind == LocalAllocate)
            {
                // ECMA-335 III.3.47: the size is a native unsigned int (an int32 is accepted).
                // A constant zero (ldc.i4.0; conv.u) is a proven NativeZero, which spills as Native.
                if (depth < 1 || (!IsNativeInteger(stack[depth - 1]) && stack[depth - 1] != I32)) return -2;
                return Push(stack, allocated, depth - 1, Native);
            }
            if (kind == LoadIndirect)
            {
                if (depth < 1 || !IsNativeInteger(stack[depth - 1])) return -2;
                return Push(stack, allocated, depth - 1, IndirectType(operand));
            }
            if (kind == StoreIndirect)
            {
                if (depth < 2 || !IsNativeInteger(stack[depth - 2]) || !IndirectValue(operand, stack[depth - 1])) return -2;
                return depth - 2;
            }
            if (kind == TypeFromHandle)
                return depth > 0 && stack[depth - 1] == PeLibrary.RuntimeTypeMetadata.Handle
                    ? Push(stack, allocated, depth - 1, PeLibrary.RuntimeTypeMetadata.Type) : -1;
            // [] -> [writer]; [writer, string-or-null] -> []; [writer] -> [] for WriteLine().
            if (kind == ConsoleWriter) return Push(stack, allocated, depth, PeLibrary.ConsoleStorage.Writer);
            if (kind == ConsoleWrite)
            {
                int writer = operand == PeLibrary.ConsoleMetadata.WriteEmptyLine ? depth - 1 : depth - 2;
                if (writer < 0 || stack[writer] != PeLibrary.ConsoleStorage.Writer) return -1;
                return writer == depth - 1 || Match(stack, depth, String) ? writer : -1;
            }
            if (kind == ClearValue) return depth;
            if (kind == NullLiteral) return Push(stack, allocated, depth, NullValue);
            if (kind == LoadEventArgsEmpty || kind == NewEventArgs) return Push(stack, allocated, depth, EventArgsReference);
            if (kind == NewRootObject) return Push(stack, allocated, depth, ObjectReference);
            if (kind == CombineHandler || kind == RemoveHandler)
                return Match(stack, depth, EventHandlerReference) && Match(stack, depth - 1, EventHandlerReference)
                    ? depth - 1 : -1;
            // The supported delegate operations already proved the result's concrete type.
            if (kind == CastHandler) return Match(stack, depth, EventHandlerReference) ? depth : -1;
            if (kind == CastClass || kind == TestClass)
            {
                if (depth < 1 || IsValueType(objects, stack[depth - 1]) ||
                    (stack[depth - 1] != NullValue && !IsReference(stack[depth - 1]))) return -1;
                return Push(stack, allocated, depth - 1, operand);
            }
            if (kind == JoinStrings)
                return Match(stack, depth, String) && Match(stack, depth - 1, String)
                    ? Push(stack, allocated, depth - 2, String) : -1;
            if (kind == StringFromChars)
                return Match(stack, depth, Chars) ? Push(stack, allocated, depth - 1, String) : -1;
            if (kind == StringLength)
                return Match(stack, depth, String) ? Push(stack, allocated, depth - 1, I32) : -1;
            if (kind == StringCharacter)
                return Match(stack, depth, I32) && Match(stack, depth - 1, String)
                    ? Push(stack, allocated, depth - 2, I32) : -1;
            if (kind == LoadFunction) return Push(stack, allocated, depth, MethodPointer);
            if (kind == NewDelegate)
            {
                if (!Match(stack, depth, MethodPointer) || depth < 2 || stack[depth - 2] != extra) return -1;
                return Push(stack, allocated, depth - 2, operand);
            }
            if (kind == InvokeDelegate)
            {
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
                if (IsValueType(objects, type) || IsValueType(objects, top)) return -2;
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
                return CallShape(stack, allocated, depth, signatures, offsets[operand], arities[operand], kind == NewObject ? 1 : 0, objects);
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
            if (kind == BaseCtor)
                return depth > 0 && stack[depth - 1] >= FirstClass && !IsInterface(objects, stack[depth - 1]) &&
                    !IsValueType(objects, stack[depth - 1]) ? depth - 1 : -1;
            if (kind == LoadStaticField) return Push(stack, allocated, depth, StackType(extra));
            if (kind == StoreStaticField)
                return depth > 0 && Assignable(stack[depth - 1], extra, objects) ? depth - 1 : -1;
            if (kind == GetField)
            {
                if (depth == 0 || !Assignable(stack[depth - 1], FieldAccess.Owner(extra), objects)) return -1;
                return Push(stack, allocated, depth - 1, StackType(FieldAccess.Storage(extra)));
            }
            if (kind == SetField)
                return depth >= 2 && Assignable(stack[depth - 2], FieldAccess.Owner(extra), objects) &&
                    Assignable(stack[depth - 1], FieldAccess.Storage(extra), objects) ? depth - 2 : -1;
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
                if (operand == 0x9a)
                {
                    if (depth < 2 || !ArrayIndex(stack, depth)) return -1;
                    int element = ArrayElement(objects, stack[depth - 2]);
                    if (element == 0 || IsValueType(objects, element)) return -2;
                    return Push(stack, allocated, depth - 2, element);
                }
                byte array = ArrayLoadType(operand);
                if (!ArrayIndex(stack, depth) || !Match(stack, depth - 1, array)) return -1;
                stack[depth - 2] = ArrayValueType(array);
                return depth - 1;
            }
            else if (kind == ArraySet)
            {
                if (operand == 0xa2)
                {
                    if (depth < 3 || !ArrayIndex(stack, depth - 1)) return -1;
                    int element = ArrayElement(objects, stack[depth - 3]);
                    if (element == 0 || IsValueType(objects, element)) return -2;
                    return Assignable(stack[depth - 1], element, objects) ? depth - 3 : -1;
                }
                byte array = ArrayStoreType(operand);
                if (depth == 0 || !Assignable(stack[depth - 1], ArrayValueType(array), objects) ||
                    !ArrayIndex(stack, depth - 1) ||
                    !Match(stack, depth - 2, array))
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
