using static PortableCompiler.BranchIl;

namespace PortableCompiler
{
    internal static class StackDepths
    {
        internal static bool Analyze(int[] records, int[] depths, int maximum)
        {
            if (depths.Length == 0)
                return false;
            var pending = new int[depths.Length];
            // Zero is unvisited. Store depth plus one to avoid filling the array.
            depths[0] = 1;
            int head = 0, tail = 1;
            while (head < tail)
            {
                int pc = pending[head++];
                int slot = pc * Width;
                int kind = records[slot];
                int depth = depths[pc] - 1;
                int operand = records[slot + 1];
                int required = 0, delta = 0;
                if (kind == Return)
                {
                    if (depth != 1) return false;
                    continue;
                }
                if (kind == Load || kind == Constant) delta = 1;
                else if (kind == Unary || (kind == Nop && operand != 0)) required = 1;
                else if (kind == Dup) { required = 1; delta = 1; }
                else if (kind == Pop || kind == Store) { required = 1; delta = -1; }
                else if (kind == Binary) { required = 2; delta = -1; }
                else if (kind == Call)
                {
                    required = records[slot + 3];
                    delta = 1 - required;
                }
                else if (kind == Conditional)
                {
                    int comparison = records[slot + 3];
                    required = comparison == 0 || comparison == 0x45 ? 1 : 2;
                    delta = -required;
                }
                if (depth < required || depth + delta > maximum)
                    return false;

                int nextDepth = depth + delta + 1;
                if (kind == Branch || kind == Conditional)
                {
                    tail = Edge(operand, nextDepth, depths, pending, tail);
                    if (tail < 0) return false;
                }
                if (kind != Branch)
                {
                    tail = Edge(records[slot + 2], nextDepth, depths, pending, tail);
                    if (tail < 0) return false;
                }
            }
            return true;
        }

        private static int Edge(int pc, int depth, int[] depths, int[] pending, int tail)
        {
            if (pc >= depths.Length) return -1;
            if (depths[pc] != 0) return depths[pc] == depth ? tail : -1;
            depths[pc] = depth;
            pending[tail] = pc;
            return tail + 1;
        }
    }
}
