using static PortableCompiler.BranchIl;

namespace PortableCompiler
{
    internal static class FrameRootLiveness
    {
        // Analyze one managed frame slot on the original decoded IL. Live bits are
        // indexed by IL offset and describe the current value before that instruction.
        // Address exposure and exceptional continuations require separate accounting.
        internal static int Analyze(int[] records, int slot, bool hasExceptionHandlers, byte[] live)
        {
            int length = records.Length / Width;
            if (live.Length != length) return -3;
            if (hasExceptionHandlers) return -2;
            for (int pc = 0; pc < length; pc++)
            {
                int at = pc * Width;
                int kind = records[at];
                if (kind == Leave || kind == Rethrow || kind == EndFinally || kind == FilterSkip ||
                    (kind == FrameAddress && records[at + 1] == slot)) return -2;
                live[pc] = 0;
            }

            // An ordinary backward must-kill, may-use analysis. Back edges can add
            // requirements at offsets visited earlier in this pass. Bits only grow.
            bool changed = true;
            while (changed)
            {
                changed = false;
                for (int pc = length - 1; pc >= 0; pc--)
                {
                    int at = pc * Width;
                    int kind = records[at];
                    if (kind == 0 || kind == Return || kind == ThrowNull || live[pc] != 0) continue;
                    int operand = records[at + 1];
                    if (kind == Store && operand == slot) continue;
                    bool used = kind == Load && operand == slot;
                    if (kind != Branch)
                    {
                        int next = records[at + 2];
                        if (next >= length) return -1;
                        if (live[next] != 0) used = true;
                    }
                    if ((kind == Branch || kind == Conditional) && live[operand] != 0) used = true;
                    if (kind == Switch)
                        for (int i = 0; i < records[at + 3]; i++)
                            if (live[SwitchTarget(records, operand, i)] != 0) used = true;
                    if (!used) continue;
                    live[pc] = 1;
                    changed = true;
                }
            }
            return 0;
        }
    }
}
