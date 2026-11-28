using PeLibrary;
using static PortableCompiler.BranchIl;

namespace PortableCompiler
{
    internal static class AggregateBoxAssignments
    {
        internal static int Bind(BundleImages bundle, int[] objects, int[] records,
            int[] frame, int result, int[] clauses, int[] diagnostic)
        {
            for (int slot = 0; slot < records.Length; slot += Width)
            {
                if (records[slot] != BoxAggregate) continue;
                int next = records[slot + 2];
                if (next >= records.Length / Width) continue;
                int target = Target(records, next * Width, frame, result);
                if (!ObjectLayouts.IsInterface(objects, target) || ValueOperands.Entry(records, next, clauses)) continue;

                // The store or return receives this exact box on its only
                // incoming edge. An ordinary object may reach any shared
                // consumer, so retain normal validation when there is a join.
                int type = records[slot + 1];
                int count = ObjectLayouts.ArgumentCount(objects, target);
                var required = new int[2 + count];
                required[0] = ObjectLayouts.AssemblyOf(objects, target);
                required[1] = ObjectLayouts.TypeDef(objects, target);
                for (int i = 0; i < count; i++) required[2 + i] = ObjectLayouts.Argument(objects, target, i);
                int status = BundleConstraints.Implements(bundle, objects, type, required, 0);
                if (status == -1 || status == -3)
                {
                    if (diagnostic.Length != 0) diagnostic[2] = slot / Width;
                    return status;
                }
                if (status <= 0) continue;

                // Both an interface and the box use eqref. Keep the same
                // allocation and copied snapshot while typing the result.
                records[slot] = BoxAggregateToInterface;
                records[slot + 1] = FieldAccess.Encode(type, target);
            }
            return 0;
        }

        private static int Target(int[] records, int slot, int[] frame, int result)
        {
            int kind = records[slot];
            if (kind == SetField || kind == SetReferenceField || kind == UpdateConstructorField)
                return FieldAccess.Storage(records[slot + 3]);
            if (kind == Store) return frame[records[slot + 1]];
            return kind == Return ? result : 0;
        }
    }
}
