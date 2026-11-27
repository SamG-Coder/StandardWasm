using static PortableCompiler.BranchIl;

namespace PortableCompiler
{
    // A traced receiver signature gives the verifier an exact CLI method
    // shape. It needs a body only when an executable use keeps that receiver.
    internal sealed class ReceiverBodyDemand
    {
        private readonly byte[] traced;

        internal ReceiverBodyDemand(int capacity) { traced = new byte[capacity]; }

        internal void Require(int function, int[] contexts)
        {
            if (contexts[function] < 0) traced[function] = 1;
        }

        internal bool Needed(int function, int[] contexts) => contexts[function] >= 0 || traced[function] != 0;

        internal void ObserveCalls(int[] records, int[] depths, int[] contexts)
        {
            for (int pc = 0; pc < depths.Length; pc++)
            {
                if (depths[pc] == 0) continue;
                int slot = pc * Width, kind = records[slot];
                if (kind == Call || kind == NewObject || kind == ConstructValue)
                    Require(records[slot + 1], contexts);
            }
        }

        internal int Revisit(int functions, int[] contexts, int[] sizes)
        {
            for (int function = 0; function < functions; function++)
                if (contexts[function] < 0 && traced[function] != 0 && sizes[function] == 0) return function;
            return -1;
        }

        internal int WriteCarriers(byte[] code, int used, int functions, int[] contexts, int[] sizes, int[] offsets)
        {
            int carrier = -1;
            for (int function = 0; function < functions; function++)
            {
                if (contexts[function] >= 0 || sizes[function] != 0) continue;
                // An undemanded signature is an interior function-index slot.
                // Preserve numbering without emitting unrelated IL or a callable path.
                if (traced[function] != 0) return -2;
                if (carrier < 0)
                {
                    if (code.Length - used < 3) return -3;
                    carrier = used;
                    used = FunctionPointerWasm.CarrierBody(code, used);
                }
                offsets[function] = carrier;
                sizes[function] = used - carrier;
            }
            return used;
        }
    }
}
