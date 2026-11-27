using static PortableCompiler.BranchWasm;
using static PortableCompiler.SignatureTypes;

namespace PortableCompiler
{
    internal static class StaticInt32Wasm
    {
        internal const int Read = 0, Write = 1, Address = 2;

        internal static int Emit(byte[] code, int used, int action, int global, int local)
        {
            code[used++] = 0x23; // global.get the permanent one-element array
            used = WasmBinary.Unsigned(code, used, global);
            used = Const(code, used, 0);
            if (action == Write)
            {
                used = Local(code, used, 0x20, local);
                return Gc(code, used, 0x0e, Ints);
            }
            // The same owner/index representation opens either a static
            // field or an array element in a ref Int32 parameter.
            used = action == Address ? Gc(code, used, 0, Int32Location) : Gc(code, used, 0x0b, Ints);
            return Local(code, used, 0x21, local);
        }
    }
}
