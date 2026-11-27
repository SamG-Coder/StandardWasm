using PeLibrary;

namespace PortableCompiler
{
    // Internal calls use the descriptor ABI where stack analysis proves it.
    // A changed signature requires a clean emission pass.
    internal sealed class RuntimeTypeCalls
    {
        private const int Width = 4, ResultSlot = -1, Body = 0, ClosedBody = 1, Declaration = 2;
        private int[] slots = new int[16];
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
            // Virtual calls bind through their slots. Instance receivers keep their nominal layout.
            if ((flags & 0x2040) != 0 || argument == 0 && (flags & 0x10) == 0) return false;
            return Add(owner, row, argument);
        }

        internal bool RequireResult(BundleImages bundle, int owner, int row, int type, int[] objects, bool specialized)
        {
            if (!RuntimeTypeLocals.IsLinkedSystemType(bundle, objects, type)) return false;
            byte[] image = bundle.Get(owner);
            var layout = new int[4];
            var heaps = new int[8];
            if (CompressedTables.FindWithHeaps(image, layout, heaps) < 0) return false;
            int flags = MethodDefinitions.ReadColumn(image, layout, row, 2);
            return (flags & 0x2040) == 0 && Add(owner, row, ResultSlot, specialized ? ClosedBody : Body);
        }

        internal bool RequireVirtualArgument(BundleImages bundle, int owner, int row, int argument, int type, int[] objects) =>
            RuntimeTypeLocals.IsLinkedSystemType(bundle, objects, type) && Add(owner, row, argument, Declaration);

        internal void ApplyVirtualArguments(BundleImages bundle, int owner, int row, int[] types, int offset, int[] objects)
        {
            for (int entry = 0; entry < used; entry += Width)
            {
                if (slots[entry + 3] != Declaration || slots[entry] != owner || slots[entry + 1] != row) continue;
                int slot = offset + 1 + slots[entry + 2];
                if (RuntimeTypeLocals.IsLinkedSystemType(bundle, objects, types[slot]))
                    types[slot] = RuntimeTypeMetadata.Type;
            }
        }

        // The dispatcher and every selected target share the descriptor ABI.
        // Persist a newly selected body demand before lowering that body.
        internal int BindVirtualSlot(BundleImages bundle, int[] objects, int expected, int[] signatures,
            int offset, int owner, int row, int argument, bool specialized)
        {
            if (signatures[offset] == expected || expected != RuntimeTypeMetadata.Type) return 0;
            if (!RuntimeTypeLocals.IsLinkedSystemType(bundle, objects, signatures[offset])) return -2;
            if (Add(owner, row, argument, specialized ? ClosedBody : Body)) return MethodCompiler.Reemit;
            signatures[offset] = expected;
            return 0;
        }

        private bool Add(int owner, int row, int argument, int kind = Body)
        {
            for (int entry = 0; entry < used; entry += Width)
                if (slots[entry] == owner && slots[entry + 1] == row && slots[entry + 2] == argument && slots[entry + 3] == kind)
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
            slots[used + 3] = kind;
            used += Width;
            return true;
        }

        internal void ApplyDescriptorSignatures(int[] bases, int[] indices, int[] offsets, int[] signatures)
        {
            for (int entry = 0; entry < used; entry += Width)
            {
                if (slots[entry + 3] != Body) continue;
                int function = indices[bases[slots[entry]] + slots[entry + 1]] - 1;
                if (function >= 0)
                    signatures[offsets[function] + 1 + slots[entry + 2]] = RuntimeTypeMetadata.Type;
            }
        }

        internal void ApplyClosedSignatures(BundleImages bundle, int[] owners, int[] rows, int[] contexts,
            MethodInstantiations instantiations, int[] offsets, int[] signatures, int[] objects, int first, int functions)
        {
            // Registry numbers and function indices can change on re-emission.
            // Match the declaring MethodDef and check the instantiated slot.
            for (int entry = 0; entry < used; entry += Width)
            {
                if (slots[entry + 3] != ClosedBody) continue;
                for (int function = first; function < functions; function++)
                {
                    if (owners[function] != slots[entry] || rows[function] != slots[entry + 1] ||
                        contexts[function] == 0 && instantiations.Arguments(function) == null) continue;
                    int slot = offsets[function] + 1 + slots[entry + 2];
                    if (RuntimeTypeLocals.IsLinkedSystemType(bundle, objects, signatures[slot]))
                        signatures[slot] = RuntimeTypeMetadata.Type;
                }
            }
        }
    }
}
