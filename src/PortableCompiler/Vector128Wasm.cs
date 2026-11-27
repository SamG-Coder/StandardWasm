namespace PortableCompiler
{
    internal static class Vector128Wasm
    {
        internal static int Zero(byte[] code, int used)
        {
            used = Instruction(code, used, 0x0c); // v128.const
            for (int lane = 0; lane < 16; lane++) code[used++] = 0;
            return used;
        }

        internal static int Instruction(byte[] code, int used, int operation)
        {
            code[used++] = 0xfd;
            return WasmBinary.Unsigned(code, used, operation);
        }
    }
}
