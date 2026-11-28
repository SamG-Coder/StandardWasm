using static PortableCompiler.BranchWasm;
using static PortableCompiler.SignatureTypes;

namespace PortableCompiler
{
    internal static class StaticValueWasm
    {
        internal static int Emit(byte[] code, int used, int[] records, int slot,
            int input, int depth, int spills, int maximum, int spillFlags)
        {
            int action = records[slot + 3], type = records[slot + BranchIl.Width + 1];
            code[used++] = 0x23; // global.get
            used = WasmBinary.Unsigned(code, used, records[slot + 1]);
            if (action == StaticReferenceWasm.Write)
            {
                used = LoadOperand(code, used, Spill(spills, maximum, spillFlags, input, depth - 1), input, type);
                return ManagedValueLocationWasm.WriteSnapshot(code, used);
            }

            // The global carries a single stable cell. A normal load takes its
            // current immutable snapshot; a managed address keeps the cell.
            int result = type;
            if (action == StaticReferenceWasm.Address) result = ValueLocation(type);
            else used = ManagedValueLocationWasm.ReadSnapshot(code, used, type);
            return Local(code, used, 0x21, Spill(spills, maximum, spillFlags, result, depth));
        }
    }
}
