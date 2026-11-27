using PeLibrary;
using static PortableCompiler.BranchWasm;
using static PortableCompiler.SignatureTypes;

namespace PortableCompiler
{
    // Address-taken native words and blittable locals have one slot for the whole frame.
    // The CLI local and every unmanaged pointer to it must observe the same bytes.
    internal static class NativeFrameWasm
    {
        internal const int Capacity = StackAllocationWasm.Capacity + 32;

        // Positive Native marks a scalar slot. A negative value is the negated
        // blittable layout plan index, leaving managed cell encodings untouched.
        internal static bool IsCell(int storage) => storage == Native || storage < 0;

        internal static int BindAddresses(BundleImages bundle, int[] objects, NativeAggregatePlans plans,
            int[] records, int[] clauses, int[] frame, int parameters, int[] cells, int[] location)
        {
            for (int at = 0; at < records.Length; at += BranchIl.Width)
            {
                if (records[at] != BranchIl.FrameAddress || !ConvertsToPointer(records, at, clauses)) continue;
                int local = records[at + 1], type = frame[local];
                if (cells[local] != 0) continue;
                if (type == Native) cells[local] = Native;
                else if (local >= parameters && ObjectLayouts.IsValueType(objects, type))
                {
                    int plan = NativeAggregates.Plan(bundle, objects, type, plans);
                    if (plan < 0) return At(location, at, plan);
                    if (NativeAggregates.Size(plans.Plans, plan) <= 0) return At(location, at, -2);
                    cells[local] = -plan;
                }
            }
            for (int at = 0; at < records.Length; at += BranchIl.Width)
            {
                if (records[at] != BranchIl.FrameAddress || !IsCell(cells[records[at + 1]])) continue;
                // A slot with a stable native address cannot also expose a managed carrier.
                if (!ConvertsToPointer(records, at, clauses)) return At(location, at, -2);
                records[at] = BranchIl.LoadFrameLocation;
                records[at + 3] = Native;
                records[records[at + 2] * BranchIl.Width] = BranchIl.Nop;
            }
            return 0;
        }

        private static bool ConvertsToPointer(int[] records, int at, int[] clauses)
        {
            int next = records[at + 2] * BranchIl.Width;
            return next < records.Length && records[next] == BranchIl.ConvertInteger &&
                (records[next + 1] == 0xe0 || records[next + 1] == 0xd3) &&
                !ValueOperands.Entry(records, next / BranchIl.Width, clauses);
        }

        private static int At(int[] location, int record, int status)
        {
            if (location.Length != 0) location[2] = record / BranchIl.Width;
            return status;
        }

        // A blittable frame slot contains no managed references. Its Wasm cell stores
        // a memory address, so it must never be published on the eqref root table.
        internal static void ExcludeNativeRoots(byte[] frameRoots, int[] cells)
        {
            for (int slot = 0; slot < cells.Length; slot++)
                if (IsCell(cells[slot]))
                    for (int at = slot; at < frameRoots.Length; at += cells.Length) frameRoots[at] = 0;
        }

        internal static int InitializeSlots(byte[] code, int used, int sp, int baseGlobal, int limit, int scratch,
            int parameters, int[] cells, int[] addresses, int[] plans)
        {
            bool initialized = false;
            for (int slot = 0; slot < cells.Length; slot++)
            {
                if (!IsCell(cells[slot])) continue;
                int address = addresses[slot];
                int size = cells[slot] == Native ? 4 : NativeAggregates.Size(plans, -cells[slot]);
                used = Local(code, Const(code, used, size), 0x21, address);
                // Allocate keeps the size in this local until it writes the result.
                used = StackAllocationWasm.Allocate(code, used, sp, baseGlobal, limit, address, address, scratch);
                if (slot < parameters) used = StoreWord(code, used, address, slot, Native);
                initialized = true;
            }
            return initialized ? Local(code, Const(code, used, 0), 0x21, scratch) : used;
        }

        internal static int LoadWord(byte[] code, int used, int address) =>
            LinearMemoryWasm.Load(code, Local(code, used, 0x20, address), 0x4d);

        internal static int StoreAggregate(byte[] code, int used, int[] plans, int[] objects, int plan, int address,
            int spilled, int source, int type, int spills, int maximum, int spillFlags, int depth)
        {
            if (source == NullValue) return NativeAggregates.Clear(code, used, plans, plan, address);
            used = ValueFieldsWasm.Materialize(code, used, type, spilled);
            used = Local(code, used, 0x21, spilled);
            return NativeAggregates.Store(code, used, plans, objects, plan, address, 0, spilled, spills, maximum, spillFlags, depth);
        }

        internal static int StoreWord(byte[] code, int used, int address, int source, int sourceType)
        {
            used = Local(code, used, 0x20, address);
            used = LoadOperand(code, used, source, sourceType, Native);
            return LinearMemoryWasm.Store(code, used, 0xdf);
        }
    }
}
