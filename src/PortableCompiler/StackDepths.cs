using static PortableCompiler.BranchIl;
using static PeLibrary.ObjectLayouts;
using static PortableCompiler.SignatureTypes;

namespace PortableCompiler
{
    internal static class StackDepths
    {
        internal static bool Analyze(int[] records, int[] depths, byte[] states, int maximum,
            byte[] frame, byte result, byte[] signatures, int[] offsets, int[] arities)
        {
            if (depths.Length == 0)
                return false;
            var pending = new int[depths.Length];
            var stack = new byte[maximum];
            depths[0] = 1;
            int head = 0, tail = 1;
            while (head < tail)
            {
                int pc = pending[head++];
                int slot = pc * BranchIl.Width;
                int kind = records[slot];
                int depth = depths[pc] - 1;
                int operand = records[slot + 1];
                for (int i = 0; i < depth; i++)
                    stack[i] = states[pc * maximum + i];
                if (kind == Return)
                {
                    if (result == Void ? depth != 0 : depth != 1 || stack[0] != result)
                        return false;
                    continue;
                }

                int next = Transition(stack, depth, kind, operand, records[slot + 3], frame,
                    signatures, offsets, arities);
                if (next < 0)
                    return false;
                if (kind == Branch || kind == Conditional)
                {
                    tail = Edge(operand, next, maximum, stack, depths, states, pending, tail);
                    if (tail < 0) return false;
                }
                if (kind != Branch)
                {
                    tail = Edge(records[slot + 2], next, maximum, stack, depths, states, pending, tail);
                    if (tail < 0) return false;
                }
            }
            return true;
        }

        private static int Transition(byte[] stack, int depth, int kind, int operand, int extra, byte[] frame,
            byte[] signatures, int[] offsets, int[] arities)
        {
            if (kind == LoadString || kind == NullString) return Push(stack, depth, String);
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
                if (!Match(stack, depth, MethodPointer) || !Match(stack, depth - 1, (byte)extra)) return -1;
                return Push(stack, depth - 2, Delegate);
            }
            if (kind == InvokeDelegate)
            {
                if (!Match(stack, depth, Delegate)) return -1;
                return Push(stack, depth - 1, I32);
            }
            if (kind == Load) return Push(stack, depth, frame[operand]);
            if (kind == Constant || kind == WideConstant)
                return Push(stack, depth, kind == WideConstant ? I64 : I32);
            if (kind == Store) return Match(stack, depth, frame[operand]) ? depth - 1 : -1;
            if (kind == Unary || kind == Narrow || kind == ConvertInteger)
            {
                if (depth < 1 || !IsInteger(stack[depth - 1])) return -1;
                if (kind != Unary)
                    stack[depth - 1] = kind == ConvertInteger && ConvertsToWide(operand) ? I64 : I32;
                return depth;
            }
            if (kind == Dup) return depth < 1 ? -1 : Push(stack, depth, stack[depth - 1]);
            if (kind == Pop) return depth - 1;
            if (kind == Binary || kind == Conditional)
            {
                int needed = kind == Binary || (extra != 0 && extra != 0x45) ? 2 : 1;
                if (depth < needed) return -1;
                byte type = stack[depth - needed];
                if (!IsInteger(type)) return -1;
                if (needed == 2 && !Match(stack, depth, kind == Binary && IsShift(operand) ? I32 : type))
                    return -1;
                int remaining = depth - needed;
                return kind == Binary ? Push(stack, remaining, IsComparison(operand) ? I32 : type) : remaining;
            }
            if (kind == Call || kind == NewObject)
            {
                int count = arities[operand], offset = offsets[operand];
                int receiver = kind == NewObject ? 1 : 0;
                int start = depth - count + receiver;
                if (start < 0)
                    return -1;
                for (int i = receiver; i < count; i++)
                    if (stack[start + i - receiver] != signatures[offset + 1 + i])
                        return -1;
                byte result = kind == NewObject ? signatures[offset + 1] : signatures[offset];
                return result == Void ? start : Push(stack, start, result);
            }
            if (kind == BaseCtor)
                return depth > 0 && stack[depth - 1] >= FirstClass ? depth - 1 : -1;
            if (kind == GetField)
            {
                if (!Match(stack, depth, (byte)(extra >> 8))) return -1;
                stack[depth - 1] = unchecked((byte)extra);
                return depth;
            }
            if (kind == SetField)
                return Match(stack, depth, unchecked((byte)extra)) &&
                    Match(stack, depth - 1, (byte)(extra >> 8)) ? depth - 2 : -1;
            if (kind == NewArray)
            {
                if (!Match(stack, depth, I32)) return -1;
                stack[depth - 1] = (byte)operand;
            }
            else if (kind == ArrayLength)
            {
                if (!Match(stack, depth, Bytes) && !Match(stack, depth, Ints)) return -1;
                stack[depth - 1] = I32;
            }
            else if (kind == ArrayGet)
            {
                byte array = operand == 0x94 ? Ints : operand == 0x93 ? Chars : Bytes;
                if (!Match(stack, depth, I32) || !Match(stack, depth - 1, array)) return -1;
                stack[depth - 2] = I32;
                return depth - 1;
            }
            else if (kind == ArraySet)
            {
                byte array = operand == 0x9e ? Ints : operand == 0x9d ? Chars : Bytes;
                if (!Match(stack, depth, I32) || !Match(stack, depth - 1, I32) ||
                    !Match(stack, depth - 2, array))
                    return -1;
                return depth - 3;
            }
            return depth;
        }

        private static bool Match(byte[] stack, int depth, byte type) =>
            depth > 0 && stack[depth - 1] == type;

        private static int Push(byte[] stack, int depth, byte type)
        {
            if (depth >= stack.Length) return -1;
            stack[depth] = type;
            return depth + 1;
        }

        private static int Edge(int pc, int count, int maximum, byte[] stack, int[] depths,
            byte[] states, int[] pending, int tail)
        {
            if (pc >= depths.Length) return -1;
            int offset = pc * maximum;
            if (depths[pc] != 0)
            {
                if (depths[pc] != count + 1) return -1;
                for (int i = 0; i < count; i++)
                    if (states[offset + i] != stack[i]) return -1;
                return tail;
            }
            depths[pc] = count + 1;
            for (int i = 0; i < count; i++)
                states[offset + i] = stack[i];
            pending[tail] = pc;
            return tail + 1;
        }
    }
}
