using static PortableCompiler.BranchIl;
using static PeLibrary.ObjectLayouts;
using static PortableCompiler.SignatureTypes;

namespace PortableCompiler
{
    internal static class StackDepths
    {
        internal static int Analyze(int[] records, int[] depths, int[] states, int maximum,
            int[] frame, int result, int[] signatures, int[] offsets, int[] arities, int[] location, int[] catchRegion,
            byte[] outgoingNulls, int[] objects, int[] virtualTypes)
        {
            if (depths.Length == 0)
                return -1;
            var pending = new int[depths.Length];
            var stack = new int[maximum];
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
                    stack[i] = states[pc * maximum + i];
                if (kind == Return)
                {
                    if (result == Void ? depth != 0 : depth != 1 || !Assignable(stack[0], result, objects))
                        return -1;
                    continue;
                }

                if (kind == ThrowNull)
                {
                    if (depth == 0) return -1;
                    if (stack[depth - 1] != NullValue) return -2;
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
                    next = Push(stack, depth - 1, EventHandlerReference);
                }
                else if (kind == VirtualCall)
                    next = CallShape(stack, depth, virtualTypes, operand, records[slot + 3], 0, objects);
                else next = Transition(stack, depth, kind, operand, records[slot + 3], frame,
                    signatures, offsets, arities, objects);
                if (next < 0)
                    return next;
                if (outgoingNulls.Length != 0)
                    for (int i = 0; i < next; i++)
                        outgoingNulls[pc * maximum + i] = stack[i] == NullValue ? (byte)1 :
                            stack[i] == NativeZero ? (byte)2 : (byte)0;
                if (kind == Branch || kind == Conditional || kind == Leave)
                {
                    head = Edge(operand, next, maximum, stack, depths, states, pending, head, objects);
                    if (head < 0) return -1;
                }
                if (kind == Switch)
                {
                    int count = records[slot + 3];
                    for (int i = 0; i < count; i++)
                    {
                        head = Edge(SwitchTarget(records, operand, i), next, maximum, stack, depths, states, pending, head, objects);
                        if (head < 0) return -1;
                    }
                }
                if (kind != Branch && kind != Leave)
                {
                    head = Edge(records[slot + 2], next, maximum, stack, depths, states, pending, head, objects);
                    if (head < 0) return -1;
                }
            }
            return 0;
        }

        private static int CallShape(int[] stack, int depth, int[] types, int offset, int arity,
            int constructed, int[] objects)
        {
            int start = depth - arity + constructed;
            if (start < 0) return -1;
            for (int i = constructed; i < arity; i++)
                if (!Assignable(stack[start + i - constructed], types[offset + 1 + i], objects)) return -1;
            int result = types[offset + constructed];
            return result == Void ? start : Push(stack, start, StackType(result));
        }

        private static int Transition(int[] stack, int depth, int kind, int operand, int extra, int[] frame,
            int[] signatures, int[] offsets, int[] arities, int[] objects)
        {
            if (kind == LoadString) return Push(stack, depth, String);
            // [] -> [writer]; [writer, string-or-null] -> []; [writer] -> [] for WriteLine().
            if (kind == ConsoleWriter) return Push(stack, depth, PeLibrary.ConsoleStorage.Writer);
            if (kind == ConsoleWrite)
            {
                int writer = operand == PeLibrary.ConsoleMetadata.WriteEmptyLine ? depth - 1 : depth - 2;
                if (writer < 0 || stack[writer] != PeLibrary.ConsoleStorage.Writer) return -1;
                return writer == depth - 1 || Match(stack, depth, String) ? writer : -1;
            }
            if (kind == ClearValue) return depth;
            if (kind == NullLiteral) return Push(stack, depth, NullValue);
            if (kind == LoadEventArgsEmpty || kind == NewEventArgs) return Push(stack, depth, EventArgsReference);
            if (kind == CombineHandler || kind == RemoveHandler)
                return Match(stack, depth, EventHandlerReference) && Match(stack, depth - 1, EventHandlerReference)
                    ? depth - 1 : -1;
            // The supported delegate operations already proved the result's concrete type.
            if (kind == CastHandler) return Match(stack, depth, EventHandlerReference) ? depth : -1;
            if (kind == CastClass || kind == TestClass)
            {
                if (depth < 1 || IsValueType(objects, stack[depth - 1]) ||
                    (stack[depth - 1] != NullValue && !IsReference(stack[depth - 1]))) return -1;
                stack[depth - 1] = operand;
                return depth;
            }
            if (kind == JoinStrings)
                return Match(stack, depth, String) && Match(stack, depth - 1, String)
                    ? Push(stack, depth - 2, String) : -1;
            if (kind == StringFromChars)
                return Match(stack, depth, Chars) ? Push(stack, depth - 1, String) : -1;
            if (kind == StringLength)
                return Match(stack, depth, String) ? Push(stack, depth - 1, I32) : -1;
            if (kind == StringCharacter)
                return Match(stack, depth, I32) && Match(stack, depth - 1, String)
                    ? Push(stack, depth - 2, I32) : -1;
            if (kind == LoadFunction) return Push(stack, depth, MethodPointer);
            if (kind == NewDelegate)
            {
                if (!Match(stack, depth, MethodPointer) || depth < 2 || stack[depth - 2] != extra) return -1;
                return Push(stack, depth - 2, operand);
            }
            if (kind == InvokeDelegate)
            {
                if (operand == EventHandlerReference)
                    return Match(stack, depth, EventArgsReference) && depth >= 3 &&
                        Assignable(stack[depth - 2], ObjectReference, objects) && Match(stack, depth - 2, EventHandlerReference)
                        ? depth - 3 : -1;
                if (!Match(stack, depth, Delegate)) return -1;
                return Push(stack, depth - 1, I32);
            }
            if (kind == Load) return Push(stack, depth, StackType(frame[operand]));
            if (kind == Constant || kind == WideConstant)
                return Push(stack, depth, kind == WideConstant ? I64 : I32);
            if (kind == SingleConstant || kind == DoubleConstant)
                return Push(stack, depth, kind == SingleConstant ? F32 : F64);
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
            if (kind == Dup) return depth < 1 ? -1 : Push(stack, depth, stack[depth - 1]);
            if (kind == Pop) return depth - 1;
            if (kind == Switch) return Match(stack, depth, I32) ? depth - 1 : -1;
            if (kind == CheckedInteger)
            {
                if (depth < 2) return -1;
                int type = stack[depth - 1];
                if (IsNativeInteger(type) || IsNativeInteger(stack[depth - 2])) return -2;
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
                        ? Push(stack, depth - 2, I32) : -2;
                }
                if (IsNativeInteger(type) || IsNativeInteger(top))
                {
                    // CLI comparisons can mix native int and int32. Both
                    // use i32 bits here, including the common ldlen test.
                    if ((!IsNativeInteger(type) && type != I32) || (!IsNativeInteger(top) && top != I32) ||
                        kind == Binary && !IsComparison(operand)) return -2;
                    return kind == Binary ? Push(stack, depth - 2, I32) : depth - needed;
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
                        ? Push(stack, depth - 2, I32) : -1;
                }
                if (IsFloat(type) || IsFloat(top))
                {
                    if (needed != 2 || !IsFloat(type) || !IsFloat(top)) return -1;
                    if (kind == Conditional) return depth - 2;
                    if (!IsComparison(operand) && operand != 0x6f && (operand < 0x6a || operand > 0x6d)) return -1;
                    return Push(stack, depth - 2, IsComparison(operand) ? I32 : FloatWidth(type, top));
                }
                if (!IsInteger(type)) return -1;
                if (needed == 2 && !Match(stack, depth, kind == Binary && IsShift(operand) ? I32 : type))
                    return -1;
                int remaining = depth - needed;
                return kind == Binary ? Push(stack, remaining, IsComparison(operand) ? I32 : type) : remaining;
            }
            if (kind == MathCall)
            {
                // Every mapped overload is T(T) or T(T, T) over one numeric stack type.
                byte type = MathWasm.Type(operand);
                int arity = MathWasm.Arity(operand);
                if (!Match(stack, depth, type) || (arity == 2 && !Match(stack, depth - 1, type))) return -1;
                return Push(stack, depth - arity, type);
            }
            if (kind == Call || kind == NewObject)
                return CallShape(stack, depth, signatures, offsets[operand], arities[operand], kind == NewObject ? 1 : 0, objects);
            if (kind == BaseCtor)
                return depth > 0 && stack[depth - 1] >= FirstClass && !IsInterface(objects, stack[depth - 1]) &&
                    !IsValueType(objects, stack[depth - 1]) ? depth - 1 : -1;
            if (kind == LoadStaticField) return Push(stack, depth, StackType(extra));
            if (kind == StoreStaticField)
                return depth > 0 && Assignable(stack[depth - 1], extra, objects) ? depth - 1 : -1;
            if (kind == GetField)
            {
                if (depth == 0 || !Assignable(stack[depth - 1], FieldAccess.Owner(extra), objects)) return -1;
                stack[depth - 1] = StackType(FieldAccess.Storage(extra));
                return depth;
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
                    stack[depth - 2] = element;
                    return depth - 1;
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

        private static bool Match(int[] stack, int depth, int type) =>
            depth > 0 && (stack[depth - 1] == type || stack[depth - 1] == NullValue && IsReference(type));

        private static bool ArrayIndex(int[] stack, int depth) =>
            depth > 0 && (stack[depth - 1] == I32 || IsNativeInteger(stack[depth - 1]));

        private static int Push(int[] stack, int depth, int type)
        {
            if (depth >= stack.Length) return -1;
            stack[depth] = type;
            return depth + 1;
        }

        private static int Edge(int pc, int count, int maximum, int[] stack, int[] depths,
            int[] states, int[] pending, int head, int[] objects)
        {
            if (pc >= depths.Length) return -1;
            int offset = pc * maximum;
            bool changed = depths[pc] == 0;
            if (changed)
            {
                depths[pc] = count + 1;
                for (int i = 0; i < count; i++)
                    states[offset + i] = stack[i];
            }
            else
            {
                if (depths[pc] != count + 1) return -1;
                for (int i = 0; i < count; i++)
                {
                    int current = states[offset + i], incoming = stack[i];
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
