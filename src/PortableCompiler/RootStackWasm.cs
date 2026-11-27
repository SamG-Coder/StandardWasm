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

        internal static byte[] MapFrame(int[] records, int[] frame, int slots)
        {
            int length = records.Length / BranchIl.Width;
            if (slots != 0 && length > int.MaxValue / slots) return null;
            var map = new byte[length * slots];
            var live = new byte[length];
            for (int slot = 0; slot < slots; slot++)
            {
                if (IsManagedLocation(frame[slot]) || IsValueLocation(frame[slot]) || IsSpan(frame[slot])) return null;
                if (!IsObjectReference(frame[slot])) continue;
                if (FrameRootLiveness.Analyze(records, slot, false, live) != 0) return null;
                for (int pc = 0; pc < length; pc++) map[pc * slots + slot] = live[pc];
            }
            return map;
        }

        internal static int ExportSize(int count) => TableExport.Length + CountExport.Length + 5 + WasmBinary.UnsignedSize(count);

        internal static int WriteExports(byte[] code, int used, int count)
        {
            used = Export(code, used, TableExport, 1, Table);
            return Export(code, used, CountExport, 3, count);
        }

        private static int Export(byte[] code, int used, string name, byte kind, int index)
        {
            code[used++] = (byte)name.Length;
            for (int i = 0; i < name.Length; i++) code[used++] = (byte)name[i];
            code[used++] = kind;
            return WasmBinary.Unsigned(code, used, index);
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
