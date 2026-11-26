using static PortableCompiler.BranchIl;
using static PortableCompiler.SignatureTypes;

namespace PortableCompiler
{
    internal static class NullSpillWasm
    {
        internal const byte NullRef = 0x71;

        internal static int Transfer(byte[] code, int used, int[] records, int pc, int[] depths,
            int[] states, byte[] outgoingNulls, int maximum, int spills, int spillFlags = 0)
        {
            int slot = pc * BranchIl.Width;
            int kind = records[slot];
            if (kind == Return || kind == Leave || kind == Rethrow || kind == EndFinally || kind == ThrowNull)
                return used;

            int operand = records[slot + 1];
            if (kind == Branch || kind == Conditional)
                used = Edge(code, used, depths, states, outgoingNulls, maximum, spills, spillFlags, pc, operand);
            if (kind == Switch)
                for (int i = 0; i < records[slot + 3]; i++)
                    used = Edge(code, used, depths, states, outgoingNulls, maximum, spills, spillFlags,
                        pc, SwitchTarget(records, operand, i));
            if (kind != Branch)
                used = Edge(code, used, depths, states, outgoingNulls, maximum, spills, spillFlags, pc, records[slot + 2]);
            return used;
        }

        private static int Edge(byte[] code, int used, int[] depths, int[] states, byte[] outgoingNulls,
            int maximum, int spills, int spillFlags, int pc, int successor)
        {
            int count = depths[successor] - 1;
            for (int i = 0; i < count; i++)
            {
                int target = states[successor * maximum + i];
                int source = outgoingNulls[pc * maximum + i];
                if (source == 0 || source == 1 && target == NullValue ||
                    source == 2 && target != NullPointer) continue;
                if (code.Length == 0)
                {
                    if (used > int.MaxValue - 8) return int.MaxValue;
                    used += 8; // ref.null, local.set and up to five bytes for its index.
                    continue;
                }

                // All successors see null in this surviving slot. Initializing
                // each required typed bank is safe before branch selection and
                // prevents a prior iteration's reference from leaking through.
                code[used++] = 0xd0;
                code[used++] = NullRef;
                code[used++] = 0x21;
                used = WasmBinary.Unsigned(code, used, BranchWasm.Spill(spills, maximum, spillFlags, target, i));
            }
            return used;
        }
    }
}
