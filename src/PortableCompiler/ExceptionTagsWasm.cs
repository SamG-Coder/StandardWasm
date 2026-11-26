namespace PortableCompiler
{
    internal static class ExceptionTagsWasm
    {
        internal const int OverflowExportSize = 12, DivideExportSize = 16;
        internal const int IndexExportSize = 19, NullExportSize = 17, InvalidCastExportSize = 15, ArgumentExportSize = 12;
        internal const int OverflowTag = 0, DivideTag = 1, IndexTag = 2, NullTag = 3, InvalidCastTag = 4, ArgumentTag = 5;
        internal const int ArgumentNullTag = 7, ArgumentNullExportSize = 16, Count = 7;
        // Decision 0054 keeps managed objects on tag 6. New fixed tags follow
        // it without shifting the previous binary indices.
        internal const int ObjectTag = 6, ObjectExportSize = 10, ObjectTagTypeBytes = 4;
        internal static int FixedTag(int ordinal) => ordinal < ObjectTag ? ordinal : ordinal + 1;

        internal static int WriteObjectTagType(byte[] output, int cursor)
        {
            output[cursor++] = 0x60; // func
            output[cursor++] = 1;
            output[cursor++] = 0x6d; // eqref: the compiler's representation of System.Object
            output[cursor++] = 0;
            return cursor;
        }

        internal static int WriteObjectExport(byte[] code, int used)
        {
            // A leading NUL keeps the tag outside the printable managed export namespace.
            code[used++] = 7;
            code[used++] = 0;
            code[used++] = (byte)'o';
            code[used++] = (byte)'b';
            code[used++] = (byte)'j';
            code[used++] = (byte)'e';
            code[used++] = (byte)'c';
            code[used++] = (byte)'t';
            code[used++] = 4; // exported tag
            code[used++] = ObjectTag;
            return used;
        }

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

        internal static int WriteArgumentExport(byte[] code, int used)
        {
            code[used++] = 9;
            code[used++] = 0;
            code[used++] = (byte)'a';
            code[used++] = (byte)'r';
            code[used++] = (byte)'g';
            code[used++] = (byte)'u';
            code[used++] = (byte)'m';
            code[used++] = (byte)'e';
            code[used++] = (byte)'n';
            code[used++] = (byte)'t';
            code[used++] = 4;
            code[used++] = ArgumentTag;
            return used;
        }

        internal static int WriteArgumentNullExport(byte[] code, int used)
        {
            const string name = "argumentNull";
            code[used++] = (byte)(1 + name.Length);
            code[used++] = 0;
            for (int i = 0; i < name.Length; i++) code[used++] = (byte)name[i];
            code[used++] = 4;
            code[used++] = ArgumentNullTag;
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
