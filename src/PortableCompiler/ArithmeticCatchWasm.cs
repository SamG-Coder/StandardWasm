using static PortableCompiler.BranchWasm;

namespace PortableCompiler
{
    internal static class ArithmeticCatchWasm
    {
        internal static int Begin(byte[] code, int used)
        {
            code[used++] = 0x02; // outer destination for divide-by-zero
            code[used++] = 0x40;
            code[used++] = 0x02; // inner destination for overflow
            code[used++] = 0x40;
            code[used++] = 0x1f; // try_table
            code[used++] = 0x40;
            code[used++] = 2;
            for (int tag = 0; tag < 2; tag++)
            {
                code[used++] = 0; // catch without an exception reference
                code[used++] = (byte)tag;
                code[used++] = (byte)tag;
            }
            return used;
        }

        internal static int End(byte[] code, int used, int pc, int[] region)
        {
            code[used++] = 0x0b; // try_table
            code[used++] = 0;    // only the catch edges may reach the destinations
            code[used++] = 0x0b; // overflow destination
            used = Dispatch(code, used, pc, region, region[ArithmeticCatch.OverflowResume], 0, 1);
            code[used++] = 0x0b; // divide-by-zero destination
            return Dispatch(code, used, pc, region, region[ArithmeticCatch.DivideResume], 1, 0);
        }

        private static int Dispatch(byte[] code, int used, int pc, int[] region, int resume, int tag, int loop)
        {
            if (resume == 0) return ArithmeticExceptionWasm.Throw(code, used, tag);
            // The dispatcher changes pc after an instruction succeeds. An
            // exception outside the interval, including in a handler, escapes.
            used = Local(code, used, 0x20, pc);
            used = Const(code, used, region[ArithmeticCatch.TryStart]);
            code[used++] = 0x6b;
            used = Const(code, used, region[ArithmeticCatch.TryLength]);
            code[used++] = 0x4f; // i32.ge_u
            used = ArithmeticExceptionWasm.ThrowIf(code, used, tag);
            used = Const(code, used, resume);
            used = Local(code, used, 0x21, pc);
            code[used++] = 0x0c;
            code[used++] = (byte)loop;
            return used;
        }
    }
}
