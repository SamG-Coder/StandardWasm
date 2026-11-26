using static PortableCompiler.BranchWasm;

namespace PortableCompiler
{
    internal static class FinallyWasm
    {
        internal static int Leave(byte[] code, int used, int pc, int target, int pcLocal, int continuationBase, int[] clauses)
        {
            int first = target, previous = -1;
            for (int clause = 0; clause < clauses.Length; clause += ExceptionClauses.Width)
            {
                if (!ExceptionClauses.IsFinally(clauses, clause) ||
                    !ExceptionClauses.InProtectedRange(clauses, clause, pc) ||
                    ExceptionClauses.InProtectedRange(clauses, clause, target)) continue;

                int handler = clauses[clause + ExceptionClauses.Resume];
                if (previous < 0)
                    first = handler;
                else
                    used = SaveTarget(code, used, continuationBase + previous, handler);

                // A leave has no pending exception. Clear any reference from a
                // previous execution of the same handler.
                previous = clause / ExceptionClauses.Width;
                code[used++] = 0xd0; // ref.null
                code[used++] = ExceptionDispatchWasm.ExceptionReference;
                used = Local(code, used, 0x21, pcLocal + 2 + previous);
            }
            if (previous >= 0) used = SaveTarget(code, used, continuationBase + previous, target);
            return Const(code, used, first);
        }

        internal static int Finish(byte[] code, int used, int exceptionLocal, int continuationLocal)
        {
            used = Local(code, used, 0x20, exceptionLocal);
            code[used++] = 0xd1; // ref.is_null
            code[used++] = 0x45; // i32.eqz
            code[used++] = 0x04; // resume unwinding with the original exception
            code[used++] = 0x40;
            used = ExceptionDispatchWasm.ThrowReference(code, used, exceptionLocal);
            code[used++] = 0x0b;
            return Local(code, used, 0x20, continuationLocal);
        }

        private static int SaveTarget(byte[] code, int used, int local, int target) =>
            Local(code, Const(code, used, target), 0x21, local);
    }
}
