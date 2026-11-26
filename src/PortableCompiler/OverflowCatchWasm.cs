using static PortableCompiler.BranchWasm;

namespace PortableCompiler
{
    internal static class OverflowCatchWasm
    {
        internal static int Begin(byte[] code, int used)
        {
            code[used++] = 0x02; // catch destination block
            code[used++] = 0x40;
            code[used++] = 0x1f; // try_table
            code[used++] = 0x40;
            code[used++] = 1;
            code[used++] = 0; // catch tag zero without an exception reference
            code[used++] = 0;
            code[used++] = 0; // destination is the enclosing block
            return used;
        }

        internal static int End(byte[] code, int used, int pc, int[] region)
        {
            code[used++] = 0x0b; // try_table
            code[used++] = 0;
            code[used++] = 0x0b; // catch destination
            // The dispatcher only changes pc after an instruction succeeds.
            // Overflow outside the protected interval must continue unwinding.
            used = Local(code, used, 0x20, pc);
            used = Const(code, used, region[OverflowCatch.TryStart]);
            code[used++] = 0x6b;
            used = Const(code, used, region[OverflowCatch.TryLength]);
            code[used++] = 0x4f; // i32.ge_u
            used = OverflowWasm.ThrowIf(code, used);
            used = Const(code, used, region[OverflowCatch.Resume]);
            used = Local(code, used, 0x21, pc);
            code[used++] = 0x0c;
            code[used++] = 0; // dispatch the handler
            return used;
        }
    }
}
