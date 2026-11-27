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
        private const int Width = 3;
        private int[] demands = new int[12];
        private int used;

        internal bool Require(BundleImages bundle, int owner, int row, int argument)
        {
            if (row < 1) return false;
            byte[] image = bundle.Get(owner);
            var layout = new int[4];
            var heaps = new int[8];
            if (CompressedTables.FindWithHeaps(image, layout, heaps) < 0 ||
                (MethodDefinitions.ReadColumn(image, layout, row, 2) & 0x2450) != 0x10 ||
                MethodDefinitions.ReadColumn(image, layout, row, 0) <= 0) return false;
            for (int at = 0; at < used; at += Width)
                if (demands[at] == owner && demands[at + 1] == row && demands[at + 2] == argument) return false;
            if (used == demands.Length)
            {
                var expanded = new int[demands.Length * 2];
                for (int i = 0; i < used; i++) expanded[i] = demands[i];
                demands = expanded;
            }
            demands[used] = owner;
            demands[used + 1] = row;
            demands[used + 2] = argument;
            used += Width;
            return true;
        }

        internal void Apply(int[] bases, int[] indices, int[] offsets, int[] signatures)
        {
            for (int at = 0; at < used; at += Width)
            {
                int function = indices[bases[demands[at]] + demands[at + 1]] - 1;
                if (function < 0) continue;
                int slot = offsets[function] + 1 + demands[at + 2];
                if (signatures[slot] == CharacterLocation) signatures[slot] = ReadOnlyCharacterLocation;
            }
        }
    }
}
