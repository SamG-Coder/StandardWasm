using PeLibrary;
using static PortableCompiler.BranchIl;

namespace PortableCompiler
{
    internal static class ArithmeticCatch
    {
        // Once validated, a clause carries the resume offset in the flags slot
        // and the supported tag mask in the metadata token slot.
        internal const int Width = ExceptionRegions.Width, Resume = 0, TryStart = 1, TryLength = 2, Mask = 5;
        private const int HandlerStart = 3, HandlerLength = 4;

        internal static int Validate(int[] records, int[] clauses, int[] location)
        {
            int length = records.Length / BranchIl.Width;
            for (int clause = 0; clause < clauses.Length; clause += Width)
            {
                int start = clauses[clause + HandlerStart], tryStart = clauses[clause + TryStart];
                int end = start + clauses[clause + HandlerLength];
                if (location.Length != 0) location[2] = start;
                if (start == 0 || !Boundary(records, start) || !Boundary(records, end) ||
                    !Boundary(records, tryStart) || !Boundary(records, tryStart + clauses[clause + TryLength]))
                    return -1;
                for (int earlier = 0; earlier < clause; earlier += Width)
                    if (!ValidPair(clauses, earlier, clause)) return -1;

                int entry = start * BranchIl.Width;
                // The generated handler resumes with an empty stack after consuming
                // the original exception operand. No other edge may target that pop.
                if (records[entry] != Pop) return -2;
                int resume = records[entry + 2];
                if (resume >= end || StartsTry(clauses, start)) return -1;
                clauses[clause + Resume] = resume;
            }

            int exceptionLocals = 1; // The dispatcher holds the caught Wasm reference here.
            for (int pc = 0; pc < length;)
            {
                int slot = pc * BranchIl.Width;
                int kind = records[slot], operand = records[slot + 1], next = records[slot + 2];
                if (location.Length != 0) location[2] = pc;
                if (kind == Leave)
                {
                    if (!Edge(clauses, pc, operand, true)) return -1;
                }
                else if (kind == Rethrow)
                {
                    int handler = InnermostHandler(clauses, pc);
                    if (handler < 0) return -1;
                    records[slot + 1] = handler / Width;
                    // A nested catch can overwrite the dispatcher scratch slot.
                    exceptionLocals = 1 + clauses.Length / Width;
                }
                else if (kind == Return)
                {
                    if (Protected(clauses, pc)) return -1;
                }
                else
                {
                    if ((kind == Branch || kind == Conditional) && !Edge(clauses, pc, operand, false)) return -1;
                    if (kind == Switch)
                        for (int i = 0; i < records[slot + 3]; i++)
                            if (!Edge(clauses, pc, SwitchTarget(records, operand, i), false)) return -1;
                    if (kind != Branch && (next >= length || !Edge(clauses, pc, next, false))) return -1;
                }
                pc = next;
            }
            return exceptionLocals;
        }

        private static int InnermostHandler(int[] clauses, int pc)
        {
            int selected = -1, shortest = int.MaxValue;
            for (int clause = 0; clause < clauses.Length; clause += Width)
            {
                int length = clauses[clause + HandlerLength];
                if (length >= shortest || !InRange(clauses, clause + HandlerStart, pc)) continue;
                shortest = length;
                selected = clause;
            }
            return selected;
        }

        internal static bool StartsTry(int[] clauses, int pc)
        {
            for (int clause = 0; clause < clauses.Length; clause += Width)
                if (pc == clauses[clause + TryStart]) return true;
            return false;
        }

        private static bool Boundary(int[] records, int offset) =>
            offset == records.Length / BranchIl.Width || records[offset * BranchIl.Width] != 0;

        private static bool ValidPair(int[] clauses, int earlier, int later)
        {
            if (SameTry(clauses, earlier, later))
                return !Overlaps(clauses, earlier + HandlerStart, later + HandlerStart);
            // Metadata must list an inner protected block before any try that encloses it.
            if (Inside(clauses, earlier, later + TryStart)) return true;
            if (Inside(clauses, later, earlier + TryStart)) return false;
            if (Inside(clauses, earlier, later + HandlerStart) || Inside(clauses, later, earlier + HandlerStart))
                return true;
            return !Overlaps(clauses, earlier + TryStart, later + TryStart) &&
                !Overlaps(clauses, earlier + TryStart, later + HandlerStart) &&
                !Overlaps(clauses, earlier + HandlerStart, later + TryStart) &&
                !Overlaps(clauses, earlier + HandlerStart, later + HandlerStart);
        }

        private static bool SameTry(int[] clauses, int a, int b) =>
            clauses[a + TryStart] == clauses[b + TryStart] && clauses[a + TryLength] == clauses[b + TryLength];

        private static bool Inside(int[] clauses, int inner, int outer) =>
            Contains(clauses, outer, inner + TryStart) && Contains(clauses, outer, inner + HandlerStart);

        private static bool Contains(int[] clauses, int outer, int inner) =>
            clauses[outer] <= clauses[inner] &&
            clauses[inner] + clauses[inner + 1] <= clauses[outer] + clauses[outer + 1];

        private static bool Overlaps(int[] clauses, int a, int b) =>
            clauses[a] < clauses[b] + clauses[b + 1] && clauses[b] < clauses[a] + clauses[a + 1];

        private static bool InRange(int[] clauses, int range, int pc) =>
            pc >= clauses[range] && pc - clauses[range] < clauses[range + 1];

        private static bool Protected(int[] clauses, int pc)
        {
            for (int clause = 0; clause < clauses.Length; clause += Width)
                if (InRange(clauses, clause + TryStart, pc) || InRange(clauses, clause + HandlerStart, pc)) return true;
            return false;
        }

        private static bool Edge(int[] clauses, int source, int target, bool leave)
        {
            for (int clause = 0; clause < clauses.Length; clause += Width)
            {
                int handler = clause + HandlerStart, region = clause + TryStart;
                bool fromHandler = InRange(clauses, handler, source), toHandler = InRange(clauses, handler, target);
                bool fromTry = InRange(clauses, region, source), toTry = InRange(clauses, region, target);
                if (toHandler && (!fromHandler || target == clauses[handler])) return false;
                if (!leave && (fromHandler && !toHandler || fromTry && !toTry)) return false;
                if (toTry && !fromTry && target != clauses[region] &&
                    (!leave || !LeaveToAssociatedTry(clauses, clause, source, target))) return false;
            }
            return true;
        }

        private static bool LeaveToAssociatedTry(int[] clauses, int destination, int source, int target)
        {
            bool associated = false;
            for (int clause = 0; clause < clauses.Length; clause += Width)
            {
                int handler = clause + HandlerStart, region = clause + TryStart;
                if (InRange(clauses, region, source) && !InRange(clauses, region, target)) return false;
                if (!InRange(clauses, handler, source) || InRange(clauses, handler, target)) continue;
                if (!SameTry(clauses, clause, destination)) return false;
                associated = true;
            }
            return associated;
        }
    }
}
