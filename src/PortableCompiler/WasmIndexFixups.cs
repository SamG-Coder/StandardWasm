using static PeLibrary.ImageBytes;

namespace PortableCompiler
{
    // Before the final indices are known, each reserved five-byte operand
    // stores the preceding fixup in the same chain.
    internal static class WasmIndexFixups
    {
        internal static int Write(byte[] code, int used, int bodyStart, int[] head)
        {
            int previous = head[0];
            head[0] = bodyStart + used + 1;
            for (int shift = 0; shift < 32; shift += 8)
                code[used++] = unchecked((byte)(previous >> shift));
            code[used++] = 0;
            return used;
        }

        // The same, for one of several chains kept side by side (no jagged arrays: the
        // compiler compiles itself).
        internal static int WriteAt(byte[] code, int used, int bodyStart, int[] heads, int chain)
        {
            int previous = heads[chain];
            heads[chain] = bodyStart + used + 1;
            for (int shift = 0; shift < 32; shift += 8)
                code[used++] = unchecked((byte)(previous >> shift));
            code[used++] = 0;
            return used;
        }

        internal static void Resolve(byte[] bodies, int head, int index)
        {
            while (head != 0)
            {
                int cursor = head - 1;
                head = unchecked((int)U32(bodies, (uint)cursor));
                for (int shift = 0; shift < 28; shift += 7)
                    bodies[cursor++] = (byte)(((index >> shift) & 0x7f) | 0x80);
                bodies[cursor] = (byte)(index >> 28);
            }
        }
    }
}
