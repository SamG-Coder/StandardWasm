using PeLibrary;
using static PortableCompiler.BranchIl;

namespace PortableCompiler
{
    internal static class ArithmeticCatch
    {
        internal const int Width = 4, TryStart = 0, TryLength = 1, OverflowResume = 2, DivideResume = 3;
        private const int ClauseTryStart = 1, ClauseTryLength = 2, HandlerStart = 3, HandlerLength = 4;

        internal static int Validate(int[] records, int[] region, int[] dispatch, int[] location)
        {
            int length = records.Length / BranchIl.Width;
            int tryStart = region[ClauseTryStart], tryLength = region[ClauseTryLength];
            if (!Boundary(records, tryStart) || !Boundary(records, tryStart + tryLength)) return -1;
            dispatch[TryStart] = tryStart;
            dispatch[TryLength] = tryLength;
            for (int clause = 0; clause < region.Length; clause += ExceptionRegions.Width)
            {
                int start = region[clause + HandlerStart], end = start + region[clause + HandlerLength];
                if (location.Length != 0) location[2] = start;
                if (region[clause + ClauseTryStart] != tryStart || region[clause + ClauseTryLength] != tryLength)
                    return -2;
                if (start == 0 || !Boundary(records, start) || !Boundary(records, end)) return -1;
                for (int earlier = 0; earlier < clause; earlier += ExceptionRegions.Width)
                {
                    int previous = region[earlier + HandlerStart];
                    if (start < previous + region[earlier + HandlerLength] && previous < end) return -1;
                }
                int entry = start * BranchIl.Width;
                // Empty Wasm tags implement handlers that discard the exception object.
                if (records[entry] != Pop) return -2;
                int resume = records[entry + 2], mask = region[clause + 5];
                if (resume >= end) return -1;
                // The CLR selects the first matching clause in metadata order.
                if ((mask & ExceptionTypes.Overflow) != 0 && dispatch[OverflowResume] == 0)
                    dispatch[OverflowResume] = resume;
                if ((mask & ExceptionTypes.DivideByZero) != 0 && dispatch[DivideResume] == 0)
                    dispatch[DivideResume] = resume;
            }

            for (int pc = 0; pc < length;)
            {
                int slot = pc * BranchIl.Width;
                int kind = records[slot], operand = records[slot + 1], next = records[slot + 2];
                if (location.Length != 0) location[2] = pc;
                int current = Segment(region, pc);
                if (kind == Leave)
                {
                    if (current == 0 || Segment(region, operand) != 0) return -2;
                }
                else if (kind == Return)
                {
                    if (current != 0) return -1;
                }
                else
                {
                    if ((kind == Branch || kind == Conditional) && !Edge(region, current, operand)) return -1;
                    if (kind == Switch)
                        for (int i = 0; i < records[slot + 3]; i++)
                            if (!Edge(region, current, SwitchTarget(records, operand, i))) return -1;
                    if (kind != Branch && (next >= length || !Edge(region, current, next))) return -1;
                }
                pc = next;
            }
            return 0;
        }

        private static bool Boundary(int[] records, int offset) =>
            offset == records.Length / BranchIl.Width || records[offset * BranchIl.Width] != 0;

        private static bool Edge(int[] region, int current, int next)
        {
            int target = Segment(region, next);
            if (target >= 2 && next == region[target - 2 + HandlerStart]) return false;
            return current == target || (current == 0 && next == region[ClauseTryStart]);
        }

        private static int Segment(int[] region, int pc)
        {
            int start = region[ClauseTryStart];
            if (pc >= start && pc - start < region[ClauseTryLength]) return 1;
            for (int clause = 0; clause < region.Length; clause += ExceptionRegions.Width)
            {
                start = region[clause + HandlerStart];
                if (pc >= start && pc - start < region[clause + HandlerLength]) return clause + 2;
            }
            return 0;
        }
    }
}
