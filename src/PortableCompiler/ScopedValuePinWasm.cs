using static PeLibrary.ObjectLayouts;
using static PortableCompiler.BranchWasm;
using static PortableCompiler.SignatureTypes;

namespace PortableCompiler
{
    internal static class ScopedValuePinWasm
    {
        internal static int CopyIn(byte[] code, int used, int[] objects, int[] plans, int plan, int type,
            int address, int temporaries, int scratch, int sp, int stackBase, int limit, int spills, int maximum,
            int spillFlags, int depth, ValueFieldProjections projections, int start)
        {
            used = Local(code, Const(code, used, 0), 0x21, address);
            used = Local(code, used, 0x20, 0);
            code[used++] = 0xd1; code[used++] = 0x45; // allow a null CLR byref to pass a zero pointer
            code[used++] = 0x04; code[used++] = 0x40;
            used = Local(code, Const(code, used, NativeAggregates.Size(plans, plan)), 0x21, address);
            used = StackAllocationWasm.Allocate(code, used, sp, stackBase, limit, address, address, scratch);
            int snapshot = temporaries + type - FirstClass;
            used = ManagedValueLocationWasm.ReadLocation(code, used, 0, type, projections, start);
            used = Local(code, used, 0x21, snapshot);
            used = NativeFrameWasm.StoreAggregate(code, used, plans, objects, plan, address,
                snapshot, type, type, spills, maximum, spillFlags, depth);
            code[used++] = 0x0b;
            return used;
        }

        internal static int CopyBack(byte[] code, int used, int[] plans, int plan, int type, int address,
            ValueFieldProjections projections, int start)
        {
            // The proven wrapper makes one non-reentrant synchronous call.
            // Publish writes even when the native return value reports failure.
            used = Local(code, used, 0x20, address);
            code[used++] = 0x04; code[used++] = 0x40;
            used = LoadOperand(code, used, 0, ValueLocation(type), ValueLocation(type));
            used = NativeAggregates.Load(code, used, plans, plan, address, 0);
            used = ManagedValueLocationWasm.WriteSnapshot(code, used, projections, start);
            code[used++] = 0x0b;
            return used;
        }
    }
}
