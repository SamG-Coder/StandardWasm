using static PortableCompiler.BranchWasm;

namespace PortableCompiler
{
    internal static class ExceptionDispatchWasm
    {
        internal const byte ExceptionReference = 0x69; // nullable exnref

        internal static int Begin(byte[] code, int used)
        {
            // Tag zero is the innermost destination. Each successive tag
            // targets the next enclosing block.
            for (int tag = 0; tag < ExceptionTagsWasm.Count; tag++)
            {
                code[used++] = 0x02;
                code[used++] = ExceptionReference;
            }
            code[used++] = 0x1f; // try_table
            code[used++] = 0x40;
            code[used++] = ExceptionTagsWasm.Count;
            for (int tag = 0; tag < ExceptionTagsWasm.Count; tag++)
            {
                code[used++] = 1; // catch_ref
                code[used++] = (byte)tag;
                code[used++] = (byte)tag;
            }
            return used;
        }

        internal static int End(byte[] code, int used, int pc, int[] region, bool saveHandlers)
        {
            code[used++] = 0x0b; // try_table
            code[used++] = 0;    // only the catch edges may reach the destinations
            for (int tag = 0; tag < ExceptionTagsWasm.Count; tag++)
            {
                code[used++] = 0x0b; // tag destination
                int loop = ExceptionTagsWasm.Count - tag - 1;
                used = Dispatch(code, used, pc, region, tag, loop, saveHandlers);
            }
            return used;
        }

        private static int Dispatch(byte[] code, int used, int pc, int[] clauses, int tag, int loop, bool saveHandlers)
        {
            int captured = pc + 1;
            used = Local(code, used, 0x21, captured);
            // pc still names the throwing instruction. Walk the ordered clauses
            // so an unmatched inner try can fall back to an enclosing one.
            for (int clause = 0; clause < clauses.Length; clause += ExceptionClauses.Width)
            {
                if (!ExceptionClauses.IsFinally(clauses, clause) &&
                    (clauses[clause + ExceptionClauses.Mask] & (1 << tag)) == 0) continue;
                used = Local(code, used, 0x20, pc);
                used = Const(code, used, clauses[clause + ExceptionClauses.TryStart]);
                code[used++] = 0x6b;
                used = Const(code, used, clauses[clause + ExceptionClauses.TryLength]);
                code[used++] = 0x49; // i32.lt_u
                code[used++] = 0x04; // if
                code[used++] = 0x40;
                if (saveHandlers)
                {
                    used = Local(code, used, 0x20, captured);
                    used = Local(code, used, 0x21, captured + 1 + clause / ExceptionClauses.Width);
                }
                used = Const(code, used, clauses[clause + ExceptionClauses.Resume]);
                used = Local(code, used, 0x21, pc);
                code[used++] = 0x0c;
                code[used++] = (byte)(loop + 1);
                code[used++] = 0x0b;
            }
            return ThrowReference(code, used, captured);
        }

        internal static int ThrowReference(byte[] code, int used, int local)
        {
            used = Local(code, used, 0x20, local);
            code[used++] = 0x0a; // throw_ref
            return used;
        }
    }
}
