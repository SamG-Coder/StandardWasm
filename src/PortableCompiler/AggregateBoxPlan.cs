namespace PortableCompiler
{
    // Descriptors for types that are actually boxed by reachable code.
    // Type tests and reflection reuse these exact module-local identities.
    internal sealed class AggregateBoxPlan
    {
        private int[] identities = new int[8];
        internal int Count;

        internal int Type(int index) => identities[2 * index];
        internal int Descriptor(int index) => identities[2 * index + 1];

        internal void Include(int type, int descriptor)
        {
            for (int i = 0; i < Count; i++)
                if (Type(i) == type) return;
            int at = 2 * Count;
            if (at == identities.Length)
            {
                var grown = new int[at * 2];
                for (int i = 0; i < at; i++) grown[i] = identities[i];
                identities = grown;
            }
            identities[at] = type;
            identities[at + 1] = descriptor;
            Count++;
        }
    }
}
