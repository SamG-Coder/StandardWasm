using static PortableCompiler.BranchIl;
using static PortableCompiler.SignatureTypes;

namespace PortableCompiler
{
    internal static class StackSpillWasm
    {
        internal const byte NullRef = 0x71;

        internal static int Transfer(byte[] code, int used, int[] records, int pc, int[] depths,
            int[] states, byte[] outgoingNulls, int[] outgoingReferences, int maximum, int spills, int spillFlags = 0)
        {
            int slot = pc * BranchIl.Width;
            int kind = records[slot];
            if (kind == Return || kind == Leave || kind == Rethrow || kind == EndFinally || kind == ThrowNull || kind == ThrowFrameworkArgument)
                return used;

            int operand = records[slot + 1];
            if (kind == Branch || kind == Conditional)
                used = Edge(code, used, depths, states, outgoingNulls, outgoingReferences, maximum, spills, spillFlags, pc, operand);
            if (kind == Switch)
                for (int i = 0; i < records[slot + 3]; i++)
                    used = Edge(code, used, depths, states, outgoingNulls, outgoingReferences, maximum, spills, spillFlags,
                        pc, SwitchTarget(records, operand, i));
            if (kind != Branch)
                used = Edge(code, used, depths, states, outgoingNulls, outgoingReferences, maximum, spills, spillFlags, pc, records[slot + 2]);
            return used;
        }

        private static int Edge(byte[] code, int used, int[] depths, int[] states, byte[] outgoingNulls,
            int[] outgoingReferences, int maximum, int spills, int spillFlags, int pc, int successor)
        {
            int count = depths[successor] - 1;
            for (int i = 0; i < count; i++)
            {
                int target = states[successor * maximum + i];
                int at = pc * maximum + i;
                int placeholder = outgoingNulls.Length == 0 ? 0 : outgoingNulls[at];
                bool materializesNull = placeholder == 1 && target != NullValue ||
                    placeholder == 2 && target == NullPointer;
                int source = outgoingReferences.Length == 0 ? 0 : outgoingReferences[at];
                bool copiesReference = source != 0 && source != target;
                if (!materializesNull && !copiesReference) continue;
                if (code.Length == 0)
                {
                    int bytes = materializesNull ? 8 : 12;
                    if (used > int.MaxValue - bytes) return int.MaxValue;
                    used += bytes;
                    continue;
                }

                // Distinct stack depths use distinct locals. Every edge copies
                // from the producer's bank before selecting the successor.
                int destination = BranchWasm.Spill(spills, maximum, spillFlags, target, i);
                if (materializesNull)
                {
                    code[used++] = 0xd0;
                    code[used++] = NullRef;
                }
                else
                {
                    int origin = BranchWasm.Spill(spills, maximum, spillFlags, source, i);
                    if (origin == destination) continue;
                    used = BranchWasm.Local(code, used, 0x20, origin);
                }
                used = BranchWasm.Local(code, used, 0x21, destination);
            }
            return used;
        }
    }
}
