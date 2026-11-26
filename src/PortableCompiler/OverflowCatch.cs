using static PortableCompiler.BranchIl;

namespace PortableCompiler
{
    internal static class OverflowCatch
    {
        internal const int Width = 7, TryStart = 1, TryLength = 2, HandlerStart = 3, HandlerLength = 4, Resume = 6;

        internal static int Validate(int[] records, int[] region, int[] location)
        {
            int length = records.Length / BranchIl.Width;
            int tryStart = region[TryStart], handlerStart = region[HandlerStart];
            int tryEnd = tryStart + region[TryLength], handlerEnd = handlerStart + region[HandlerLength];
            if (!Boundary(records, tryStart) || !Boundary(records, tryEnd) ||
                !Boundary(records, handlerStart) || !Boundary(records, handlerEnd) || handlerStart == 0)
                return -1;
            int slot = handlerStart * BranchIl.Width;
            if (location.Length != 0) location[2] = handlerStart;
            // The empty Wasm tag can represent a discarded exception operand.
            // A handler that stores or examines the object needs managed EH values.
            if (records[slot] != Pop) return -2;
            region[Resume] = records[slot + 2];
            if (region[Resume] >= handlerEnd) return -1;

            for (int pc = 0; pc < length;)
            {
                slot = pc * BranchIl.Width;
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

        private static bool Edge(int[] region, int current, int next) =>
            next != region[HandlerStart] && (current == Segment(region, next) ||
                (current == 0 && next == region[TryStart]));

        private static int Segment(int[] region, int pc)
        {
            int start = region[TryStart];
            if (pc >= start && pc - start < region[TryLength]) return 1;
            start = region[HandlerStart];
            return pc >= start && pc - start < region[HandlerLength] ? 2 : 0;
        }
    }
}
