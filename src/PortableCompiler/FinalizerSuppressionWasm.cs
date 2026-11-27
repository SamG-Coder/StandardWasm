using static PortableCompiler.BranchWasm;
using static PortableCompiler.ReachabilityWasm;

namespace PortableCompiler
{
    internal static class FinalizerSuppressionWasm
    {
        internal const int Table = 7, GlobalKey = int.MinValue + 21;
        internal const int Suppress = FinalizerQueueWasm.End, Consume = Suppress + 1, End = Consume + 1;

        internal static int Write(byte[] code, int used, int entry, int registered, int pending, int suppressed)
        {
            if (code.Length - used < 256) return -3;
            code[used++] = 1; code[used++] = 1; code[used++] = 0x7f; // cursor
            used = entry == Suppress ? Set(code, used, registered, pending, suppressed) : Take(code, used, suppressed);
            code[used++] = 0x0b;
            return used;
        }

        private static int Set(byte[] code, int used, int registered, int pending, int suppressed)
        {
            used = GuardNull(code, used, 0, ExceptionTagsWasm.ArgumentNullTag);
            used = Find(code, used, Table, suppressed);
            used = CompareEnd(code, used, suppressed);
            code[used++] = 0x45; // an existing marker makes repeated suppression idempotent
            code[used++] = 0x04; code[used++] = 0x40;
            code[used++] = 0x0f;
            code[used++] = 0x0b;

            // The probe tracks suppression on scheduled receivers. Keep no
            // strong marker for arbitrary objects or an already drained queue.
            used = Find(code, used, FinalizerQueueWasm.RegisteredTable, registered);
            used = CompareEnd(code, used, registered);
            code[used++] = 0x04; code[used++] = 0x40;
            used = Find(code, used, FinalizerQueueWasm.PendingTable, pending);
            used = CompareEnd(code, used, pending);
            code[used++] = 0x04; code[used++] = 0x40;
            code[used++] = 0x0f;
            code[used++] = 0x0b;
            code[used++] = 0x0b;
            return Push(code, used, Table, suppressed, 0);
        }

        private static int Take(byte[] code, int used, int suppressed)
        {
            used = Find(code, used, Table, suppressed);
            used = CompareEnd(code, used, suppressed);
            code[used++] = 0x04; code[used++] = 0x40;
            used = Const(code, used, 0);
            code[used++] = 0x0f;
            code[used++] = 0x0b;

            // Replace the marker with the last one and clear the retired slot.
            used = Index(code, used, 0x23, suppressed);
            used = Const(code, used, 1);
            code[used++] = 0x6b;
            used = Index(code, used, 0x24, suppressed);
            used = Local(code, used, 0x20, 1);
            used = Index(code, used, 0x23, suppressed);
            used = Index(code, used, 0x25, Table);
            used = Index(code, used, 0x26, Table);
            used = Index(code, used, 0x23, suppressed);
            used = Null(code, used);
            used = Index(code, used, 0x26, Table);
            return Const(code, used, 1);
        }

        private static int Find(byte[] code, int used, int table, int count)
        {
            used = Const(code, used, 0);
            used = Local(code, used, 0x21, 1);
            used = Loop(code, used);
            used = StopAtGlobal(code, used, 1, count);
            used = Local(code, used, 0x20, 1);
            used = Index(code, used, 0x25, table);
            used = Local(code, used, 0x20, 0);
            code[used++] = 0xd3; // ref.eq
            code[used++] = 0x0d; code[used++] = 1;
            used = Advance(code, used, 1);
            return EndLoop(code, used);
        }

        private static int CompareEnd(byte[] code, int used, int count)
        {
            used = Local(code, used, 0x20, 1);
            used = Index(code, used, 0x23, count);
            code[used++] = 0x46;
            return used;
        }
    }
}
