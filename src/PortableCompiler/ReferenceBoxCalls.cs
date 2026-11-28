using static PeLibrary.ObjectLayouts;
using static PortableCompiler.BranchIl;
using static PortableCompiler.SignatureTypes;

namespace PortableCompiler
{
    internal static class ReferenceBoxCalls
    {
        internal static void Bind(int[] objects, int[] records, int[] clauses,
            int[] arities, int[] offsets, int[] signatures, int[] virtualTypes)
        {
            for (int slot = 0; slot < records.Length; slot += Width)
            {
                if (records[slot] != WidenReference || records[slot + 1] == ObjectReference) continue;
                int next = records[slot + 2];
                if (next >= records.Length / Width) continue;
                int call = next * Width, target = 0;
                if (records[call] == Call)
                {
                    int function = records[call + 1];
                    if (arities[function] != 0) target = signatures[offsets[function] + arities[function]];
                }
                else if (records[call] == VirtualCall && records[call + 3] > 1)
                    target = virtualTypes[records[call + 1] + records[call + 3]];

                // Only this original reference can reach the final call argument
                // on a single incoming edge. Shared consumers rely on normal joins.
                if (IsInterface(objects, target) && Assignable(records[slot + 1], target, objects) &&
                    !ValueOperands.Entry(records, next, clauses))
                    records[slot + 3] = target;
            }
        }
    }
}
