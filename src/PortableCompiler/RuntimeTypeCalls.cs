using PeLibrary;

namespace PortableCompiler
{
    // Internal direct calls can use the descriptor ABI for verified arguments
    // and results. A changed signature requires a clean emission pass.
    internal sealed class RuntimeTypeCalls
    {
        private const int Width = 3, ResultSlot = -1;
        private int[] slots = new int[12];
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
            return Add(owner, row, argument);
        }

        internal bool RequireResult(BundleImages bundle, int owner, int row, int type, int[] objects)
        {
            if (!RuntimeTypeLocals.IsLinkedSystemType(bundle, objects, type)) return false;
            byte[] image = bundle.Get(owner);
            var layout = new int[4];
            var heaps = new int[8];
            if (CompressedTables.FindWithHeaps(image, layout, heaps) < 0) return false;
            int flags = MethodDefinitions.ReadColumn(image, layout, row, 2);
            return (flags & 0x2040) == 0 && Add(owner, row, ResultSlot);
        }

        private bool Add(int owner, int row, int argument)
        {
            for (int entry = 0; entry < used; entry += Width)
                if (slots[entry] == owner && slots[entry + 1] == row && slots[entry + 2] == argument)
                    return false;
            if (used == slots.Length)
            {
                var grown = new int[slots.Length * 2];
                for (int entry = 0; entry < used; entry++) grown[entry] = slots[entry];
                slots = grown;
            }
            slots[used] = owner;
            slots[used + 1] = row;
            slots[used + 2] = argument;
            used += Width;
            return true;
        }

        internal void ApplyDescriptorSignatures(int[] bases, int[] indices, int[] offsets, int[] signatures)
        {
            for (int entry = 0; entry < used; entry += Width)
            {
                int function = indices[bases[slots[entry]] + slots[entry + 1]] - 1;
                if (function >= 0)
                    signatures[offsets[function] + 1 + slots[entry + 2]] = RuntimeTypeMetadata.Type;
            }
        }
    }
}
