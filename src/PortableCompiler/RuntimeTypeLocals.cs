using PeLibrary;
using static PortableCompiler.BranchIl;

namespace PortableCompiler
{
    // A linked System.Type local can hold the compiler's descriptor when
    // every explicit write is known to come from that representation.
    internal static class RuntimeTypeLocals
    {
        internal static void Specialize(BundleImages bundle, int[] records, int[] frame, int parameters, int locals,
            int[] objects, int[] clauses, int[] signatures, int[] offsets, int[] virtualTypes)
        {
            if (locals == 0 || bundle.Length == 0) return;
            bool hasDescriptor = false;
            for (int slot = 0; slot < records.Length && !hasDescriptor; slot += Width)
                hasDescriptor = ProducesDescriptor(records, slot, frame, clauses, signatures, offsets, virtualTypes);
            if (!hasDescriptor) return;

            int[] writes = null;
            int previous = -1;
            for (int slot = 0; slot < records.Length; slot += Width)
            {
                int kind = records[slot];
                if (kind == 0) continue;
                if (kind == Store || kind == FrameAddress)
                {
                    int local = records[slot + 1] - parameters;
                    if (local >= 0 && local < locals && frame[local + parameters] >= ObjectLayouts.FirstClass)
                    {
                        bool fallthrough = kind == Store && previous >= 0 &&
                            records[previous + 2] * Width == slot &&
                            !ValueOperands.Entry(records, slot / Width, clauses);
                        int producer = fallthrough ? records[previous] : 0;
                        if (writes == null) writes = new int[locals];
                        if (fallthrough && ProducesDescriptor(records, previous, frame, clauses, signatures, offsets, virtualTypes)) writes[local] = writes[local] | 1;
                        else if (producer != NullLiteral) writes[local] = writes[local] | 2;
                    }
                }
                previous = slot;
            }

            if (writes == null) return;
            for (int local = 0; local < locals; local++)
            {
                int slot = local + parameters;
                if (writes[local] == 1 && IsLinkedSystemType(bundle, objects, frame[slot]))
                    frame[slot] = RuntimeTypeMetadata.Type;
            }
        }

        private static bool ProducesDescriptor(int[] records, int slot, int[] frame, int[] clauses,
            int[] signatures, int[] offsets, int[] virtualTypes)
        {
            bool readsDefinition = false;
            // A definition getter on a proven descriptor returns a descriptor.
            // Its nominal virtual override can return an ordinary Type reference.
            while (records[slot] == VirtualCall && records[slot + Width + 2] == RuntimeTypeGenericDefinition)
            {
                readsDefinition = true;
                slot = RuntimeTypeRelations.Previous(records, slot, clauses);
                if (slot < 0) return false;
            }
            int kind = records[slot];
            return kind == TypeFromHandle || kind == GetObjectType ||
                kind == VirtualCall && virtualTypes[records[slot + 1]] == RuntimeTypeMetadata.Type ||
                kind == Call && signatures[offsets[records[slot + 1]]] == RuntimeTypeMetadata.Type ||
                readsDefinition && kind == Load && frame[records[slot + 1]] == RuntimeTypeMetadata.Type;
        }

        internal static bool IsLinkedSystemType(BundleImages bundle, int[] objects, int type)
        {
            if (type < ObjectLayouts.FirstClass || type - ObjectLayouts.FirstClass >= objects[0]) return false;
            int owner = ObjectLayouts.AssemblyOf(objects, type);
            if (owner < 0 || owner >= bundle.Count) return false;
            byte[] image = bundle.Get(owner);
            var layout = new int[4];
            var heaps = new int[8];
            var counts = new int[45];
            if (CompressedTables.FindWithHeaps(image, layout, heaps) < 0) return false;
            int first = MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts);
            return first >= 0 && PlatformAssemblyIdentity.IsCoreLibDefinition(image, heaps, layout, counts) &&
                ObjectLayouts.NamedDefinition(image, heaps, layout, counts, first, ObjectLayouts.TypeDef(objects, type), "System", "Type");
        }
    }
}
