namespace PortableCompiler
{
    internal static class OverflowWasm
    {
        internal const int ExportSize = 12;

        internal static int WriteExport(byte[] code, int used)
        {
            // The leading NUL reserves this name outside the printable managed manifest namespace.
            code[used++] = 9;
            code[used++] = 0;
            code[used++] = (byte)'o';
            code[used++] = (byte)'v';
            code[used++] = (byte)'e';
            code[used++] = (byte)'r';
            code[used++] = (byte)'f';
            code[used++] = (byte)'l';
            code[used++] = (byte)'o';
            code[used++] = (byte)'w';
            code[used++] = 4; // exported tag
            code[used++] = 0; // tag index
            return used;
        }

        internal static int ThrowIf(byte[] code, int used)
        {
            code[used++] = 0x04; // if
            code[used++] = 0x40; // void
            code[used++] = 0x08; // throw
            code[used++] = 0;    // overflow tag index
            code[used++] = 0x0b;
            return used;
        }
    }
}
