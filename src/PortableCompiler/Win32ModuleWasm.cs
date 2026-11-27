using static PortableCompiler.BranchWasm;
using static PortableCompiler.SignatureTypes;

namespace PortableCompiler
{
    internal static class Win32ModuleWasm
    {
        internal const int TypeBytes = 5, BodyCapacity = 64;
        private static string Name(int import) => import == Win32ModuleBinding.Module ? "getModuleHandleW" : "createActCtxW";

        internal static int ImportEntryBytes(int type, int import) =>
            Win32ErrorWasm.Module.Length + Name(import).Length + 3 + WasmBinary.UnsignedSize(type);

        internal static int WriteType(byte[] output, int cursor)
        {
            output[cursor++] = 0x60;
            output[cursor++] = 1;
            output[cursor++] = 0x7f;
            output[cursor++] = 1;
            output[cursor++] = 0x7f;
            return cursor;
        }

        internal static int WriteEntry(byte[] output, int cursor, int type, int import)
        {
            cursor = Win32ErrorWasm.WriteName(output, cursor, Win32ErrorWasm.Module);
            cursor = Win32ErrorWasm.WriteName(output, cursor, Name(import));
            output[cursor++] = 0;
            return WasmBinary.Unsigned(output, cursor, type);
        }

        internal static int WriteBody(byte[] code, int used, int input, int result, int import, int hostImports, int initializer)
        {
            code[used++] = 0; // no declared locals
            if (initializer >= 0)
            {
                code[used++] = 0x10;
                used = WasmBinary.Unsigned(code, used, hostImports + initializer);
            }
            if (input == Native) used = Local(code, used, 0x20, 0);
            else
            {
                // The value's default null representation means a zero PCWSTR.
                used = ValueFieldsWasm.Materialize(code, used, input, 0);
                used = Gc(code, used, 2, input);
                code[used++] = 0; // sole native field
            }
            code[used++] = 0x10;
            used = WasmBinary.Unsigned(code, used, import);
            if (result != Native) used = Gc(code, used, 0, result); // HMODULE or HANDLE
            code[used++] = 0x0b;
            return used;
        }
    }
}
