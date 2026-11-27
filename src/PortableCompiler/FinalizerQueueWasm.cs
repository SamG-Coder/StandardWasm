using static PortableCompiler.BranchWasm;
using static PortableCompiler.ReachabilityWasm;

namespace PortableCompiler
{
    // Registrations are strong Wasm references but are excluded from the mark
    // seeds until they are pending. This lets their original fields survive.
    internal static class FinalizerQueueWasm
    {
        internal const int RegisteredTable = 5, PendingTable = 6;
        internal const int RegisteredKey = int.MinValue + 19, PendingKey = int.MinValue + 20;
        internal const int Register = ReachabilityWasm.End, Discover = Register + 1, Pop = Discover + 1, RunNext = Pop + 1, End = RunNext + 1;

        internal static int Write(byte[] code, int used, int entry, int first, int registered, int pending)
        {
            if (code.Length - used < 512) return -3;
            if (entry == Register) used = RegisterReceiver(code, used, registered);
            else if (entry == Discover) used = DiscoverReceivers(code, used, first, registered, pending);
            else used = PopReceiver(code, used, pending);
            code[used++] = 0x0b;
            return used;
        }

        private static int RegisterReceiver(byte[] code, int used, int registered)
        {
            code[used++] = 0;
            used = Local(code, used, 0x20, 0);
            code[used++] = 0xd1; // ref.is_null
            code[used++] = 0x04; code[used++] = 0x40;
            code[used++] = 0x0f;
            code[used++] = 0x0b;
            return Push(code, used, RegisteredTable, registered, 0);
        }

        private static int DiscoverReceivers(byte[] code, int used, int first, int registered, int pending)
        {
            const int cursor = 0, kept = 1, discovered = 2, receiver = 3;
            code[used++] = 2; code[used++] = 3; code[used++] = 0x7f;
            code[used++] = 1; code[used++] = 0x6d;
            used = Call(code, used, first + Mark);
            code[used++] = 0x1a; // drop the mark count

            // Reserve the exact required capacity before changing either queue.
            // Both passes use the same snapshot, including previous pending roots.
            used = Loop(code, used);
            used = StopAtGlobal(code, used, cursor, registered);
            used = ReadRegistered(code, used, cursor);
            used = Call(code, used, first + Contains);
            code[used++] = 0x45; // i32.eqz
            code[used++] = 0x04; code[used++] = 0x40;
            used = Advance(code, used, discovered);
            code[used++] = 0x0b;
            used = Advance(code, used, cursor);
            used = EndLoop(code, used);
            used = ReservePending(code, used, pending, discovered, first + Clear);

            used = Const(code, used, 0);
            used = Local(code, used, 0x21, cursor);
            used = Loop(code, used);
            used = StopAtGlobal(code, used, cursor, registered);
            used = ReadRegistered(code, used, cursor);
            used = Local(code, used, 0x22, receiver);
            used = Call(code, used, first + Contains);
            code[used++] = 0x04; code[used++] = 0x40;
            used = Local(code, used, 0x20, kept);
            used = Local(code, used, 0x20, receiver);
            used = Index(code, used, 0x26, RegisteredTable);
            used = Advance(code, used, kept);
            code[used++] = 0x05; // else: move the registration to pending
            used = Push(code, used, PendingTable, pending, receiver);
            code[used++] = 0x0b;
            used = Advance(code, used, cursor);
            used = EndLoop(code, used);

            used = Local(code, used, 0x20, kept);
            used = Null(code, used);
            used = Index(code, used, 0x23, registered);
            used = Local(code, used, 0x20, kept);
            code[used++] = 0x6b; // i32.sub
            used = TableOp(code, used, 17, RegisteredTable); // clear the old suffix
            used = Local(code, used, 0x20, kept);
            used = Index(code, used, 0x24, registered);
            used = Call(code, used, first + Clear);
            return Local(code, used, 0x20, discovered);
        }

        private static int ReadRegistered(byte[] code, int used, int cursor)
        {
            used = Local(code, used, 0x20, cursor);
            return Index(code, used, 0x25, RegisteredTable);
        }

        private static int ReservePending(byte[] code, int used, int pending, int discovered, int clear)
        {
            used = Needed(code, used, pending, discovered);
            used = TableOp(code, used, 16, PendingTable); // table.size
            code[used++] = 0x4b; // i32.gt_u
            code[used++] = 0x04; code[used++] = 0x40;
            used = Null(code, used);
            used = Needed(code, used, pending, discovered);
            used = TableOp(code, used, 16, PendingTable);
            code[used++] = 0x6b;
            used = TableOp(code, used, 15, PendingTable); // table.grow
            used = Const(code, used, -1);
            code[used++] = 0x46;
            code[used++] = 0x04; code[used++] = 0x40;
            used = Call(code, used, clear);
            code[used++] = 0x00;
            code[used++] = 0x0b;
            code[used++] = 0x0b;
            return used;
        }

        private static int Needed(byte[] code, int used, int pending, int discovered)
        {
            used = Index(code, used, 0x23, pending);
            used = Local(code, used, 0x20, discovered);
            code[used++] = 0x6a;
            return used;
        }

        private static int PopReceiver(byte[] code, int used, int pending)
        {
            // The probe exposes dequeue order only for deterministic testing.
            code[used++] = 1; code[used++] = 1; code[used++] = 0x7f;
            used = Index(code, used, 0x23, pending);
            code[used++] = 0x45;
            code[used++] = 0x04; code[used++] = 0x40;
            used = Null(code, used);
            code[used++] = 0x0f;
            code[used++] = 0x0b;
            used = Index(code, used, 0x23, pending);
            used = Const(code, used, 1);
            code[used++] = 0x6b;
            used = Local(code, used, 0x22, 0);
            used = Index(code, used, 0x24, pending);
            used = Local(code, used, 0x20, 0);
            used = Index(code, used, 0x25, PendingTable);
            used = Local(code, used, 0x20, 0);
            used = Null(code, used);
            return Index(code, used, 0x26, PendingTable);
        }
    }
}
