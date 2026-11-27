using static PortableCompiler.BranchWasm;
using static PortableCompiler.SignatureTypes;

namespace PortableCompiler
{
    internal static class RootStackWasm
    {
        // Keep delegate, pin and function pointer table indices stable.
        internal const int Table = 3;
        internal const int GlobalKey = int.MinValue + 17;
        private const string TableExport = "__root_probe_table";
        private const string CountExport = "__root_probe_count";
        private const string EdgeCountExport = "__root_probe_edge_count";
        private const string EdgeReadExport = "__root_probe_edge_read";
        private const string MarkExport = "__root_probe_mark";
        private const string ContainsExport = "__root_probe_contains";
        private const string ClearExport = "__root_probe_clear";
        private const string RegisterExport = "__root_probe_register";
        private const string DiscoverExport = "__root_probe_discover";
        private const string PopExport = "__root_probe_pop";
        private const string RunNextExport = "__root_probe_run_next";
        internal const int ExportCount = 11;

        internal static bool IsTraced(int type) => IsObjectReference(type) || IsManagedLocation(type) ||
            IsValueLocation(type) || IsSpan(type);

        internal static byte[] MapFrame(int[] records, int[] frame, int slots, bool protectedRegion,
            int parameters = 0, int[] filters = null)
        {
            int length = records.Length / BranchIl.Width;
            if (slots != 0 && length > int.MaxValue / slots) return null;
            var map = new byte[length * slots];
            var live = new byte[length];
            for (int slot = 0; slot < slots; slot++)
            {
                if (!IsTraced(frame[slot])) continue;
                // Verified filter scratch locals are never assigned by the emitted method.
                if (filters != null && slot >= parameters && ExceptionFilters.LocalKind(filters, slot - parameters) != 0) continue;
                bool conservative = protectedRegion;
                for (int pc = 0; pc < length && !conservative; pc++)
                {
                    int at = pc * BranchIl.Width;
                    conservative = records[at] == BranchIl.FrameAddress && records[at + 1] == slot;
                }
                // An address can survive independently of the slot's last direct use.
                // Protected frames also keep the slot through exceptional continuations.
                if (!conservative && FrameRootLiveness.Analyze(records, slot, false, live) != 0) return null;
                for (int pc = 0; pc < length; pc++) map[pc * slots + slot] = conservative ? (byte)1 : live[pc];
            }
            return map;
        }

        internal static int ExportSize(int count, int walker) => ExportSize(TableExport, Table) + ExportSize(CountExport, count) +
            ExportSize(EdgeCountExport, walker) + ExportSize(EdgeReadExport, walker + 1) +
            ExportSize(MarkExport, walker + ReachabilityWasm.Mark) + ExportSize(ContainsExport, walker + ReachabilityWasm.Contains) +
            ExportSize(ClearExport, walker + ReachabilityWasm.Clear) + ExportSize(RegisterExport, walker + FinalizerQueueWasm.Register) +
            ExportSize(DiscoverExport, walker + FinalizerQueueWasm.Discover) + ExportSize(PopExport, walker + FinalizerQueueWasm.Pop) +
            ExportSize(RunNextExport, walker + FinalizerQueueWasm.RunNext);

        private static int ExportSize(string name, int index) => name.Length + 2 + WasmBinary.UnsignedSize(index);

        internal static int WriteExports(byte[] code, int used, int count, int walker)
        {
            used = Export(code, used, TableExport, 1, Table);
            used = Export(code, used, CountExport, 3, count);
            used = Export(code, used, EdgeCountExport, 0, walker);
            used = Export(code, used, EdgeReadExport, 0, walker + 1);
            used = Export(code, used, MarkExport, 0, walker + ReachabilityWasm.Mark);
            used = Export(code, used, ContainsExport, 0, walker + ReachabilityWasm.Contains);
            used = Export(code, used, ClearExport, 0, walker + ReachabilityWasm.Clear);
            used = Export(code, used, RegisterExport, 0, walker + FinalizerQueueWasm.Register);
            used = Export(code, used, DiscoverExport, 0, walker + FinalizerQueueWasm.Discover);
            used = Export(code, used, PopExport, 0, walker + FinalizerQueueWasm.Pop);
            return Export(code, used, RunNextExport, 0, walker + FinalizerQueueWasm.RunNext);
        }

        private static int Export(byte[] code, int used, string name, byte kind, int index)
        {
            code[used++] = (byte)name.Length;
            for (int i = 0; i < name.Length; i++) code[used++] = (byte)name[i];
            code[used++] = kind;
            return WasmBinary.Unsigned(code, used, index);
        }

        internal static int CountExceptionRoots(int[] clauses, int pc)
        {
            int count = 0;
            for (int clause = 0; clause < clauses.Length; clause += ExceptionClauses.Width)
                if (ExceptionClauses.InHandler(clauses, clause, pc)) count++;
            return count;
        }

        // Rethrow and endfinally use hidden payload locals. Nested handlers may
        // have several active payloads, each visible only within its own handler.
        internal static int PublishExceptionRoots(byte[] code, int used, int[] clauses, int pc,
            int frame, int index, int objectBase)
        {
            for (int clause = 0; clause < clauses.Length; clause += ExceptionClauses.Width)
                if (ExceptionClauses.InHandler(clauses, clause, pc))
                    used = Store(code, used, frame, index++, objectBase + 1 + clause / ExceptionClauses.Width);
            return used;
        }

        // A precise type can run its initializer before the method's first IL instruction.
        // Publish its live arguments while the initializer runs inside the cleanup region.
        internal static int CallInitializerWithRoots(byte[] code, int used, int count, int frame,
            byte[] frameRoots, int parameters, int[] cells, int function)
        {
            int slots = 0;
            for (int parameter = 0; parameter < parameters; parameter++) slots += frameRoots[parameter];
            if (slots == 0) return TypeInitializerWasm.CallInitializer(code, used, function);
            used = Enter(code, used, count, frame, slots);
            int root = 0;
            for (int parameter = 0; parameter < parameters; parameter++)
            {
                if (frameRoots[parameter] == 0) continue;
                int reference = cells == null || cells[parameter] == 0 ? parameter : cells[parameter];
                used = Store(code, used, frame, root++, reference);
            }
            used = TypeInitializerWasm.CallInitializer(code, used, function);
            return Leave(code, used, count, frame);
        }

        // Initializer failure currently traps, bypassing the cleanup in every caller.
        internal static int ResetAfterInitializerFailure(byte[] code, int used, int count)
        {
            used = Const(code, used, 0);
            code[used++] = 0xd0; code[used++] = 0x6d; // ref.null eq
            used = Global(code, used, 0x23, count);
            code[used++] = 0xfc; code[used++] = 17; code[used++] = Table; // table.fill
            used = Const(code, used, 0);
            return Global(code, used, 0x24, count);
        }

        internal static int SaveBase(byte[] code, int used, int count, int frame)
        {
            used = Global(code, used, 0x23, count);
            return Local(code, used, 0x21, frame);
        }

        internal static int Enter(byte[] code, int used, int count, int frame, int roots)
        {
            used = Global(code, used, 0x23, count);
            used = Local(code, used, 0x22, frame);
            used = Const(code, used, roots);
            code[used++] = 0x6a; // i32.add
            code[used++] = 0xfc; code[used++] = 16; code[used++] = Table; // table.size
            code[used++] = 0x4b; // i32.gt_u
            code[used++] = 0x04; code[used++] = 0x40;
            code[used++] = 0xd0; code[used++] = 0x6d; // ref.null eq
            used = Local(code, used, 0x20, frame);
            used = Const(code, used, roots);
            code[used++] = 0x6a;
            code[used++] = 0xfc; code[used++] = 16; code[used++] = Table;
            code[used++] = 0x6b; // i32.sub
            code[used++] = 0xfc; code[used++] = 15; code[used++] = Table; // table.grow
            used = Const(code, used, -1);
            code[used++] = 0x46; // i32.eq
            code[used++] = 0x04; code[used++] = 0x40;
            code[used++] = 0x00; // unreachable when the engine cannot grow the root stack
            code[used++] = 0x0b;
            code[used++] = 0x0b;
            used = Local(code, used, 0x20, frame);
            used = Const(code, used, roots);
            code[used++] = 0x6a;
            return Global(code, used, 0x24, count);
        }

        internal static int Store(byte[] code, int used, int frame, int index, int reference)
        {
            used = Local(code, used, 0x20, frame);
            used = Const(code, used, index);
            code[used++] = 0x6a;
            used = Local(code, used, 0x20, reference);
            code[used++] = 0x26; code[used++] = Table; // table.set
            return used;
        }

        internal static int Leave(byte[] code, int used, int count, int frame)
        {
            used = Local(code, used, 0x20, frame);
            code[used++] = 0xd0; code[used++] = 0x6d;
            used = Global(code, used, 0x23, count);
            used = Local(code, used, 0x20, frame);
            code[used++] = 0x6b;
            code[used++] = 0xfc; code[used++] = 17; code[used++] = Table; // table.fill
            used = Local(code, used, 0x20, frame);
            return Global(code, used, 0x24, count);
        }

        private static int Global(byte[] code, int used, byte instruction, int index)
        {
            code[used++] = instruction;
            return WasmBinary.Unsigned(code, used, index);
        }
    }
}
