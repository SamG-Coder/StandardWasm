using static PortableCompiler.BranchIl;
using static PortableCompiler.SignatureTypes;

namespace PortableCompiler
{
    internal static class ValueObjectOperands
    {
        internal static void LowerManaged(int[] records, int[] depths, int[] states, int maximum)
        {
            for (int pc = 0; pc < depths.Length; pc++)
            {
                int slot = pc * Width, kind = records[slot], depth = depths[pc];
                if (depth == 0 || kind != LoadObject && kind != StoreObject && kind != InitializeNative) continue;
                int address = pc * maximum + depth - (kind == StoreObject ? 3 : 2);
                int location = ValueLocation(records[slot + 1]);
                if (states[address] != location) continue;
                records[slot] = kind == LoadObject ? LoadManagedCell : kind == StoreObject ? StoreManagedCell : ClearManagedCell;
                records[slot + 1] = 0;
                records[slot + 3] = location;
            }
        }
    }
}
