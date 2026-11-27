using PeLibrary;
using static PortableCompiler.BranchWasm;
using static PortableCompiler.SignatureTypes;

namespace PortableCompiler
{
    internal static class StaticReferenceWasm
    {
        internal const int Read = 0, Write = 1, Address = 2;

        internal static int Emit(byte[] code, int used, int[] records, int slot, int[] objects,
            int input, int depth, int spills, int maximum, int spillFlags)
        {
            int action = records[slot + 3], type = records[slot + BranchIl.Width + 1];
            int vector = records[slot + BranchIl.Width + 2];
            code[used++] = 0x23; // global.get the permanent one-element reference array
            used = WasmBinary.Unsigned(code, used, records[slot + 1]);
            used = Const(code, used, 0);
            if (action == Write)
            {
                used = LoadOperand(code, used, Spill(spills, maximum, spillFlags, input, depth - 1), input, type);
                return Gc(code, used, 0x0e, vector);
            }
            int result = type;
            if (action == Address)
            {
                result = LocationFromIdentity(objects, type);
                used = Gc(code, used, 0, result);
            }
            else
            {
                used = Gc(code, used, 0x0b, vector);
                used = ManagedLocationWasm.CastCellValue(code, used, ObjectLayouts.Representation(objects, type));
            }
            return Local(code, used, 0x21, Spill(spills, maximum, spillFlags, result, depth));
        }
    }
}
