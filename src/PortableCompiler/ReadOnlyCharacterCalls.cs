using PeLibrary;
using static PortableCompiler.SignatureTypes;

namespace PortableCompiler
{
    // A direct caller can lend a readonly character location to a managed
    // body. Reverify that body's original IL with the stricter parameter
    // type so reads can forward while writes and escapes remain invalid.
    internal sealed class ReadOnlyCharacterCalls
    {
        internal const int Target = 6, Argument = 7, Local = 8, MismatchWidth = 9;
        private ReadOnlyCharacterDemand[] demands = new ReadOnlyCharacterDemand[4];
        private int used;

        internal bool Require(BundleImages bundle, int owner, int row, int argument, int[] methodArguments)
        {
            if (row < 1 || !StableArguments(methodArguments)) return false;
            byte[] image = bundle.Get(owner);
            var layout = new int[4];
            var heaps = new int[8];
            if (CompressedTables.FindWithHeaps(image, layout, heaps) < 0 ||
                (MethodDefinitions.ReadColumn(image, layout, row, 2) & 0x2450) != 0x10 ||
                MethodDefinitions.ReadColumn(image, layout, row, 0) <= 0) return false;
            for (int at = 0; at < used; at++)
                if (demands[at].Matches(owner, row, argument, methodArguments)) return false;
            if (used == demands.Length)
            {
                var expanded = new ReadOnlyCharacterDemand[demands.Length * 2];
                for (int i = 0; i < used; i++) expanded[i] = demands[i];
                demands = expanded;
            }
            demands[used++] = new ReadOnlyCharacterDemand(owner, row, argument, methodArguments);
            return true;
        }

        internal void Apply(int[] bases, int[] indices, int[] owners, int[] rows, int[] contexts,
            int[] offsets, int[] signatures, MethodInstantiations instantiations, int functions)
        {
            for (int at = 0; at < used; at++)
            {
                ReadOnlyCharacterDemand demand = demands[at];
                int function = demand.MethodArguments == null
                    ? indices[bases[demand.Owner] + demand.Row] - 1
                    : instantiations.Find(owners, rows, contexts, functions, demand.Owner, demand.Row, 0, demand.MethodArguments);
                if (function < 0) continue;
                int slot = offsets[function] + 1 + demand.Argument;
                if (signatures[slot] == CharacterLocation) signatures[slot] = ReadOnlyCharacterLocation;
            }
        }

        private static bool StableArguments(int[] arguments)
        {
            if (arguments == null) return true;
            // Registry-local identities may be renumbered across re-emission.
            // Only stable nominal keys may persist in a specialization demand.
            for (int index = 0; index < arguments.Length; index++)
                if (!GenericScalar.IsScalar(arguments[index]) && arguments[index] != Vector128Word) return false;
            return true;
        }
    }

    internal sealed class ReadOnlyCharacterDemand
    {
        internal readonly int Owner, Row, Argument;
        internal readonly int[] MethodArguments;

        internal ReadOnlyCharacterDemand(int owner, int row, int argument, int[] methodArguments)
        {
            Owner = owner;
            Row = row;
            Argument = argument;
            MethodArguments = methodArguments;
        }

        internal bool Matches(int owner, int row, int argument, int[] methodArguments)
        {
            if (Owner != owner || Row != row || Argument != argument) return false;
            if (MethodArguments == null || methodArguments == null) return MethodArguments == methodArguments;
            if (MethodArguments.Length != methodArguments.Length) return false;
            for (int index = 0; index < methodArguments.Length; index++)
                if (MethodArguments[index] != methodArguments[index]) return false;
            return true;
        }
    }
}
