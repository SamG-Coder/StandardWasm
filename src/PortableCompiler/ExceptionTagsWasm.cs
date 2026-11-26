namespace PortableCompiler
{
    internal static class ExceptionTagsWasm
    {
        internal const int OverflowExportSize = 12, DivideExportSize = 16;
        internal const int IndexExportSize = 19, NullExportSize = 17, InvalidCastExportSize = 15;
        internal const int OverflowTag = 0, DivideTag = 1, IndexTag = 2, NullTag = 3, InvalidCastTag = 4, Count = 5;

        internal static int WriteOverflowExport(byte[] code, int used)
        {
            // A leading NUL keeps the tag outside the printable managed export namespace.
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
            code[used++] = OverflowTag;
            return used;
        }

        internal static int WriteDivideExport(byte[] code, int used)
        {
            // A leading NUL keeps the tag outside the printable managed export namespace.
            code[used++] = 13;
            code[used++] = 0;
            code[used++] = (byte)'d';
            code[used++] = (byte)'i';
            code[used++] = (byte)'v';
            code[used++] = (byte)'i';
            code[used++] = (byte)'d';
            code[used++] = (byte)'e';
            code[used++] = (byte)'B';
            code[used++] = (byte)'y';
            code[used++] = (byte)'Z';
            code[used++] = (byte)'e';
            code[used++] = (byte)'r';
            code[used++] = (byte)'o';
            code[used++] = 4; // exported tag
            code[used++] = DivideTag;
            return used;
        }

        internal static int WriteIndexExport(byte[] code, int used)
        {
            code[used++] = 16;
            code[used++] = 0;
            code[used++] = (byte)'i';
            code[used++] = (byte)'n';
            code[used++] = (byte)'d';
            code[used++] = (byte)'e';
            code[used++] = (byte)'x';
            code[used++] = (byte)'O';
            code[used++] = (byte)'u';
            code[used++] = (byte)'t';
            code[used++] = (byte)'O';
            code[used++] = (byte)'f';
            code[used++] = (byte)'R';
            code[used++] = (byte)'a';
            code[used++] = (byte)'n';
            code[used++] = (byte)'g';
            code[used++] = (byte)'e';
            code[used++] = 4; // exported tag
            code[used++] = IndexTag;
            return used;
        }

        internal static int WriteNullExport(byte[] code, int used)
        {
            code[used++] = 14;
            code[used++] = 0;
            code[used++] = (byte)'n';
            code[used++] = (byte)'u';
            code[used++] = (byte)'l';
            code[used++] = (byte)'l';
            code[used++] = (byte)'R';
            code[used++] = (byte)'e';
            code[used++] = (byte)'f';
            code[used++] = (byte)'e';
            code[used++] = (byte)'r';
            code[used++] = (byte)'e';
            code[used++] = (byte)'n';
            code[used++] = (byte)'c';
            code[used++] = (byte)'e';
            code[used++] = 4; // exported tag
            code[used++] = NullTag;
            return used;
        }

        internal static int WriteInvalidCastExport(byte[] code, int used)
        {
            code[used++] = 12;
            code[used++] = 0;
            code[used++] = (byte)'i';
            code[used++] = (byte)'n';
            code[used++] = (byte)'v';
            code[used++] = (byte)'a';
            code[used++] = (byte)'l';
            code[used++] = (byte)'i';
            code[used++] = (byte)'d';
            code[used++] = (byte)'C';
            code[used++] = (byte)'a';
            code[used++] = (byte)'s';
            code[used++] = (byte)'t';
            code[used++] = 4;
            code[used++] = InvalidCastTag;
            return used;
        }

        internal static int ThrowIf(byte[] code, int used) => ThrowIf(code, used, OverflowTag);

        internal static int ThrowIf(byte[] code, int used, int tag)
        {
            code[used++] = 0x04; // if
            code[used++] = 0x40; // void
            used = Throw(code, used, tag);
            code[used++] = 0x0b;
            return used;
        }

        internal static int Throw(byte[] code, int used, int tag)
        {
            code[used++] = 0x08;
            code[used++] = (byte)tag;
            return used;
        }
    }
}
