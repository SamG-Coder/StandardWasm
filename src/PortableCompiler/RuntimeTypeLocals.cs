using PeLibrary;
using static PortableCompiler.BranchIl;

namespace PortableCompiler
{
    // A linked System.Type local can hold the compiler's descriptor when
    // every explicit write is known to come from that representation.
    internal static class RuntimeTypeLocals
    {
        internal static void Specialize(BundleImages bundle, int[] records, int[] frame, int parameters, int locals,
            int[] objects, int[] clauses)
        {
            if (locals == 0 || bundle.Length == 0) return;
            bool hasDescriptor = false;
            for (int slot = 0; slot < records.Length && !hasDescriptor; slot += Width)
                hasDescriptor = records[slot] == TypeFromHandle || records[slot] == GetObjectType;
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
                        if (producer == TypeFromHandle || producer == GetObjectType) writes[local] = writes[local] | 1;
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

        private static bool IsLinkedSystemType(BundleImages bundle, int[] objects, int type)
        {
            int owner = ObjectLayouts.AssemblyOf(objects, type);
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
