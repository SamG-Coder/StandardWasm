using static PortableCompiler.BranchWasm;
using static PortableCompiler.SignatureTypes;

namespace PortableCompiler
{
    internal static class ReachabilityWasm
    {
        internal const int Table = 4;
        internal const int GlobalKey = int.MinValue + 18;
        internal const int Mark = 2, Contains = 3, Clear = 4, Add = 5, End = 6;

        internal static int Write(byte[] code, int used, int entry, int first, int roots, int marks, int[] globals)
        {
            if (code.Length - used < 256 || globals[0] > (code.Length - used - 256) / 12) return -3;
            if (entry == Mark) used = Walk(code, used, first, roots, marks, globals);
            else if (entry == Contains) used = Find(code, used, marks);
            else if (entry == Clear) used = Reset(code, used, marks);
            else used = Append(code, used, marks, first + Contains);
            code[used++] = 0x0b;
            return used;
        }

        private static int Walk(byte[] code, int used, int first, int roots, int marks, int[] globals)
        {
            // Locals cursor, edge and limit are i32; the current receiver is eqref.
            code[used++] = 2; code[used++] = 3; code[used++] = 0x7f;
            code[used++] = 1; code[used++] = 0x6d;
            used = Call(code, used, first + Clear);
            for (int index = 0; index < globals[0]; index++)
            {
                // Positive rows are CLI storage. Synthetic reference globals have
                // independent lifetime rules and need their own root provider.
                if (StaticFields.Owner(globals, index) < 0 || StaticFields.Row(globals, index) <= 0 ||
                    !IsObjectReference(StaticFields.Storage(globals, index))) continue;
                used = Index(code, used, 0x23, index);
                used = Call(code, used, first + Add);
            }

            used = Loop(code, used);
            used = StopAtGlobal(code, used, 0, roots);
            used = Local(code, used, 0x20, 0);
            used = Index(code, used, 0x25, RootStackWasm.Table);
            used = Call(code, used, first + Add);
            used = Advance(code, used, 0);
            used = EndLoop(code, used);

            used = Const(code, used, 0);
            used = Local(code, used, 0x21, 0);
            // The marked prefix doubles as a worklist. Appending a distinct
            // receiver extends the loop without revisiting an alias or cycle.
            used = Loop(code, used);
            used = StopAtGlobal(code, used, 0, marks);
            used = Local(code, used, 0x20, 0);
            used = Index(code, used, 0x25, Table);
            used = Local(code, used, 0x22, 3);
            used = Call(code, used, first); // typed edge count
            used = Local(code, used, 0x21, 2);
            used = Const(code, used, 0);
            used = Local(code, used, 0x21, 1);
            used = Loop(code, used);
            used = Local(code, used, 0x20, 1);
            used = Local(code, used, 0x20, 2);
            used = StopAtLimit(code, used);
            used = Local(code, used, 0x20, 3);
            used = Local(code, used, 0x20, 1);
            used = Call(code, used, first + 1); // typed edge read
            used = Call(code, used, first + Add);
            used = Advance(code, used, 1);
            used = EndLoop(code, used);
            used = Advance(code, used, 0);
            used = EndLoop(code, used);
            return Index(code, used, 0x23, marks);
        }

        private static int Find(byte[] code, int used, int marks)
        {
            code[used++] = 1; code[used++] = 1; code[used++] = 0x7f; // cursor
            used = Loop(code, used);
            used = StopAtGlobal(code, used, 1, marks);
            used = Local(code, used, 0x20, 1);
            used = Index(code, used, 0x25, Table);
            used = Local(code, used, 0x20, 0);
            code[used++] = 0xd3; // ref.eq
            code[used++] = 0x04; code[used++] = 0x40;
            used = Const(code, used, 1);
            code[used++] = 0x0f;
            code[used++] = 0x0b;
            used = Advance(code, used, 1);
            used = EndLoop(code, used);
            return Const(code, used, 0);
        }

        private static int Reset(byte[] code, int used, int marks)
        {
            code[used++] = 0;
            used = Const(code, used, 0);
            used = Null(code, used);
            used = Index(code, used, 0x23, marks);
            used = TableOp(code, used, 17); // table.fill
            used = Const(code, used, 0);
            return Index(code, used, 0x24, marks);
        }

        private static int Append(byte[] code, int used, int marks, int contains)
        {
            code[used++] = 0;
            used = Local(code, used, 0x20, 0);
            code[used++] = 0xd1; // ref.is_null
            used = Local(code, used, 0x20, 0);
            used = Call(code, used, contains);
            code[used++] = 0x72; // i32.or
            code[used++] = 0x04; code[used++] = 0x40;
            code[used++] = 0x0f;
            code[used++] = 0x0b;
            used = Index(code, used, 0x23, marks);
            used = TableOp(code, used, 16); // table.size
            code[used++] = 0x46;
            code[used++] = 0x04; code[used++] = 0x40;
            used = Null(code, used);
            used = Const(code, used, 1);
            used = TableOp(code, used, 15); // table.grow
            used = Const(code, used, -1);
            code[used++] = 0x46;
            code[used++] = 0x04; code[used++] = 0x40;
            code[used++] = 0x00;
            code[used++] = 0x0b;
            code[used++] = 0x0b;
            used = Index(code, used, 0x23, marks);
            used = Local(code, used, 0x20, 0);
            used = Index(code, used, 0x26, Table);
            used = Index(code, used, 0x23, marks);
            used = Const(code, used, 1);
            code[used++] = 0x6a;
            return Index(code, used, 0x24, marks);
        }

        private static int Loop(byte[] code, int used)
        {
            code[used++] = 0x02; code[used++] = 0x40;
            code[used++] = 0x03; code[used++] = 0x40;
            return used;
        }

        private static int EndLoop(byte[] code, int used)
        {
            code[used++] = 0x0c; code[used++] = 0;
            code[used++] = 0x0b; code[used++] = 0x0b;
            return used;
        }

        private static int StopAtGlobal(byte[] code, int used, int local, int global)
        {
            used = Local(code, used, 0x20, local);
            used = Index(code, used, 0x23, global);
            return StopAtLimit(code, used);
        }

        private static int StopAtLimit(byte[] code, int used)
        {
            code[used++] = 0x4f; // i32.ge_u
            code[used++] = 0x0d; code[used++] = 1;
            return used;
        }

        private static int Advance(byte[] code, int used, int local)
        {
            used = Local(code, used, 0x20, local);
            used = Const(code, used, 1);
            code[used++] = 0x6a;
            return Local(code, used, 0x21, local);
        }

        private static int Null(byte[] code, int used)
        {
            code[used++] = 0xd0; code[used++] = 0x6d;
            return used;
        }

        private static int TableOp(byte[] code, int used, byte op)
        {
            code[used++] = 0xfc; code[used++] = op; code[used++] = Table;
            return used;
        }

        private static int Call(byte[] code, int used, int function) => Index(code, used, 0x10, function);

        private static int Index(byte[] code, int used, byte opcode, int index)
        {
            code[used++] = opcode;
            return WasmBinary.Unsigned(code, used, index);
        }
    }
}
