using static PortableCompiler.BranchWasm;

namespace PortableCompiler
{
    internal static class MonitorWasm
    {
        private const string Module = "standardwasm:threading", Member = "changeMonitorDepth";
        internal const int TypeBytes = 6, Capacity = 64;

        internal static int EntryBytes(int type) => Module.Length + Member.Length + 3 + WasmBinary.UnsignedSize(type);

        internal static int WriteType(byte[] output, int cursor)
        {
            output[cursor++] = 0x60;
            output[cursor++] = 2;
            output[cursor++] = 0x6d; // eqref
            output[cursor++] = 0x7f; // depth delta
            output[cursor++] = 1;
            output[cursor++] = 0x7f; // zero means an unmatched Exit
            return cursor;
        }

        internal static int WriteEntry(byte[] output, int cursor, int type)
        {
            cursor = Win32ErrorWasm.WriteName(output, cursor, Module);
            cursor = Win32ErrorWasm.WriteName(output, cursor, Member);
            output[cursor++] = 0;
            return WasmBinary.Unsigned(output, cursor, type);
        }

        internal static int Emit(byte[] code, int used, int target, int action, int import)
        {
            if (action > 0)
            {
                used = Local(code, used, 0x20, action - 1);
                used = ExceptionTagsWasm.ThrowIf(code, used, ExceptionTagsWasm.ArgumentTag);
            }
            used = GuardNull(code, used, target, ExceptionTagsWasm.ArgumentNullTag);
            used = Local(code, used, 0x20, target);
            used = Const(code, used, action < 0 ? -1 : 1);
            code[used++] = 0x10;
            used = WasmBinary.Unsigned(code, used, import);
            if (action < 0)
            {
                code[used++] = 0x45;
                return ExceptionTagsWasm.ThrowIf(code, used, ExceptionTagsWasm.SynchronizationLockTag);
            }
            code[used++] = 0x1a; // an Enter always succeeds on the owning agent
            if (action == 0) return used;
            used = Const(code, used, 1);
            return Local(code, used, 0x21, action - 1);
        }
    }
}
