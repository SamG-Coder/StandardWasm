namespace PortableCompiler
{
    internal static class RuntimeDebuggerWasm
    {
        private const string Module = "standardwasm:runtime", Member = "isDebuggerAttached";
        internal const int TypeBytes = 4, BodyCapacity = 16;

        internal static int EntryBytes(int type) => Module.Length + Member.Length + 3 + WasmBinary.UnsignedSize(type);

        internal static int WriteType(byte[] output, int used)
        {
            output[used++] = 0x60;
            output[used++] = 0;
            output[used++] = 1;
            output[used++] = 0x7f;
            return used;
        }

        internal static int WriteEntry(byte[] output, int used, int type)
        {
            used = Win32ErrorWasm.WriteName(output, used, Module);
            used = Win32ErrorWasm.WriteName(output, used, Member);
            output[used++] = 0;
            return WasmBinary.Unsigned(output, used, type);
        }

        internal static int WriteBody(byte[] code, int used, int import, int hostImports, int initializer)
        {
            code[used++] = 0; // no declared locals
            if (initializer >= 0)
            {
                code[used++] = 0x10;
                used = WasmBinary.Unsigned(code, used, hostImports + initializer);
            }
            code[used++] = 0x10;
            used = WasmBinary.Unsigned(code, used, import);
            code[used++] = 0x0b;
            return used;
        }
    }
}
