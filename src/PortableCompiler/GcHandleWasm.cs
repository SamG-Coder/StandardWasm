using PeLibrary;
using static PortableCompiler.BranchWasm;
using static PortableCompiler.ReachabilityWasm;
using static PortableCompiler.SignatureTypes;

namespace PortableCompiler
{
    // Each pair in this instance-local managed table holds [target, occupied].
    // The even public token is the pair's starting index plus two. Allocation
    // never recycles a token, and freeing clears both references. A live null
    // target is distinct from an empty entry without reserving a target value.
    internal static class GcHandleWasm
    {
        internal const int Table = FinalizerSuppressionWasm.Table + 1;
        internal const int GlobalKey = int.MinValue + 26;
        private const int Normal = 2;

        internal static bool Present(int[] globals)
        {
            for (int index = 0; index < globals[0]; index++)
                if (StaticFields.Owner(globals, index) == 0 && StaticFields.Row(globals, index) == GlobalKey)
                    return true;
            return false;
        }

        internal static bool Matches(int operation, int[] signatures, int offset, int arity)
        {
            int result = signatures[offset];
            if (operation == GcHandleBinding.Allocate)
                return arity == 2 && result == Native &&
                    signatures[offset + 1] == ObjectLayouts.ObjectReference && signatures[offset + 2] == I32;
            if (operation == GcHandleBinding.Set)
                return arity == 2 && result == Void &&
                    signatures[offset + 1] == Native && signatures[offset + 2] == ObjectLayouts.ObjectReference;
            return arity == 1 && signatures[offset + 1] == Native &&
                result == (operation == GcHandleBinding.Get ? ObjectLayouts.ObjectReference : Void);
        }

        internal static int WriteBody(byte[] code, int used, int operation, int count, int failure)
        {
            if (code.Length - used < 192) return -3;
            code[used++] = 1; code[used++] = 1; code[used++] = 0x7f; // slot index
            if (operation == GcHandleBinding.Allocate) used = Allocate(code, used, count, failure);
            else
            {
                int slot = operation == GcHandleBinding.Set ? 2 : 1;
                used = Decode(code, used, slot, count, failure);
                used = Local(code, used, 0x20, slot);
                if (operation == GcHandleBinding.Get) used = Index(code, used, 0x25, Table);
                else if (operation == GcHandleBinding.Set)
                {
                    used = Local(code, used, 0x20, 1);
                    used = Index(code, used, 0x26, Table);
                }
                else
                {
                    used = Null(code, used);
                    used = Const(code, used, 2);
                    used = TableOp(code, used, 17, Table); // table.fill
                }
            }
            code[used++] = 0x0b;
            return used;
        }

        private static int Allocate(byte[] code, int used, int count, int unsupported)
        {
            const int value = 0, kind = 1, slot = 2;
            used = Local(code, used, 0x20, kind);
            used = Const(code, used, Normal);
            code[used++] = 0x47; // i32.ne
            used = FailIf(code, used, unsupported);

            // Advance the token only after both table entries are available.
            used = Null(code, used);
            used = Const(code, used, 2);
            used = TableOp(code, used, 15, Table); // table.grow
            used = Local(code, used, 0x22, slot);
            used = Const(code, used, -1);
            code[used++] = 0x46; // i32.eq
            // Engine table exhaustion follows other Wasm allocation failures.
            used = FailIf(code, used, -1);
            used = Local(code, used, 0x20, slot);
            used = Local(code, used, 0x20, value);
            used = Index(code, used, 0x26, Table);
            used = Local(code, used, 0x20, slot);
            used = Const(code, used, 1);
            code[used++] = 0x6a;
            used = Const(code, used, 1);
            code[used++] = 0xfb; code[used++] = 0x1c; // ref.i31 occupancy marker
            used = Index(code, used, 0x26, Table);
            used = Local(code, used, 0x20, slot);
            used = Const(code, used, 2);
            code[used++] = 0x6a;
            used = Index(code, used, 0x24, count);
            return Index(code, used, 0x23, count);
        }

        private static int Decode(byte[] code, int used, int slot, int count, int invalid)
        {
            const int token = 0;
            used = Local(code, used, 0x20, token);
            used = Const(code, used, 2);
            code[used++] = 0x49; // i32.lt_u
            used = Local(code, used, 0x20, token);
            used = Index(code, used, 0x23, count);
            code[used++] = 0x4b; // i32.gt_u
            code[used++] = 0x72;
            used = Local(code, used, 0x20, token);
            used = Const(code, used, 1);
            code[used++] = 0x71;
            code[used++] = 0x72;
            used = FailIf(code, used, invalid);
            used = Local(code, used, 0x20, token);
            used = Const(code, used, 2);
            code[used++] = 0x6b;
            used = Local(code, used, 0x22, slot);
            used = Const(code, used, 1);
            code[used++] = 0x6a;
            used = Index(code, used, 0x25, Table);
            code[used++] = 0xd1; // ref.is_null marks a released entry
            return FailIf(code, used, invalid);
        }

        private static int FailIf(byte[] code, int used, int failure)
        {
            code[used++] = 0x04; code[used++] = 0x40;
            if (failure >= 0) used = Call(code, used, failure);
            code[used++] = 0x00;
            code[used++] = 0x0b;
            return used;
        }
    }
}
