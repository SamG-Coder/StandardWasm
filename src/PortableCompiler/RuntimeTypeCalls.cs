using PeLibrary;

namespace PortableCompiler
{
    // Internal direct-call parameters may use the descriptor ABI if all
    // reachable call sites agree. Changing one requires a clean emission pass.
    internal sealed class RuntimeTypeCalls
    {
        private const int Width = 3;
        private int[] parameters = new int[12];
        private int used;

        internal bool Require(BundleImages bundle, int owner, int row, int argument, int type, int[] objects)
        {
            if (type < ObjectLayouts.FirstClass || !RuntimeTypeLocals.IsLinkedSystemType(bundle, objects, type))
                return false;
            byte[] image = bundle.Get(owner);
            var layout = new int[4];
            var heaps = new int[8];
            if (CompressedTables.FindWithHeaps(image, layout, heaps) < 0) return false;
            int flags = MethodDefinitions.ReadColumn(image, layout, row, 2);
            // An instance receiver and virtual targets retain their nominal ABI.
            if ((flags & 0x2040) != 0 || argument == 0 && (flags & 0x10) == 0) return false;
            for (int entry = 0; entry < used; entry += Width)
                if (parameters[entry] == owner && parameters[entry + 1] == row && parameters[entry + 2] == argument)
                    return false;
            if (used == parameters.Length)
            {
                var grown = new int[parameters.Length * 2];
                for (int entry = 0; entry < used; entry++) grown[entry] = parameters[entry];
                parameters = grown;
            }
            parameters[used] = owner;
            parameters[used + 1] = row;
            parameters[used + 2] = argument;
            used += Width;
            return true;
        }

        internal void Apply(int[] bases, int[] indices, int[] offsets, int[] signatures)
        {
            for (int entry = 0; entry < used; entry += Width)
            {
                int function = indices[bases[parameters[entry]] + parameters[entry + 1]] - 1;
                if (function >= 0)
                    signatures[offsets[function] + 1 + parameters[entry + 2]] = RuntimeTypeMetadata.Type;
            }
        }
    }
}
