using static PortableCompiler.BranchWasm;

namespace PortableCompiler
{
    // Stage 151 (decision 0079): Console.ReadLine() through two "standardwasm:console" imports,
    // a separate group from the Stage 110 writers, so modules that only write keep their imports.
    //   readLine() -> i32   the next line's length in UTF-16 code units, or -1 at EOF.
    //                       The host may suspend here (JS Promise Integration) until a
    //                       line or EOF is available; the module sees an ordinary call.
    //   readUnit(i32) -> i32 code unit i of the line readLine last returned.
    // The CLR's StreamReader.ReadLine strips "\r\n", "\n" or "\r"; the host does the same.
    internal static class ConsoleInputWasm
    {
        internal const int ImportCount = 2, ReadLineFunction = 0, ReadUnitFunction = 1;
        internal const int TypeBytes = 4 + 5; // () -> i32 and (i32) -> i32
        // Calls, the null branch and the copy loop for one ReadLine call site.
        internal const int ReadCapacity = 96;
        private const int ModuleNameLength = 20;

        // [] -> [String or null]. first is the function index of readLine.
        internal static int ReadLine(byte[] code, int used, int first, int characters, int index, int length, int result)
        {
            code[used++] = 0x10;
            used = WasmBinary.Unsigned(code, used, first + ReadLineFunction);
            used = Local(code, used, 0x22, length);
            used = Const(code, used, 0);
            code[used++] = 0x48; // i32.lt_s: EOF
            code[used++] = 0x04; code[used++] = 0x40;
            code[used++] = 0xd0; code[used++] = 0x05; // ref.null String
            used = Local(code, used, 0x21, result);
            code[used++] = 0x05; // else
            used = Local(code, used, 0x20, length);
            code[used++] = 0xfb; code[used++] = 7; code[used++] = 4; // array.new_default Char[]
            used = Local(code, used, 0x21, characters);
            used = Const(code, used, 0);
            used = Local(code, used, 0x21, index);
            code[used++] = 0x02; code[used++] = 0x40; // block
            code[used++] = 0x03; code[used++] = 0x40; // loop over code units in order
            used = Local(code, used, 0x20, index);
            used = Local(code, used, 0x20, length);
            code[used++] = 0x4f; // i32.ge_u
            code[used++] = 0x0d; code[used++] = 1;
            used = Local(code, used, 0x20, characters);
            used = Local(code, used, 0x20, index);
            used = Local(code, used, 0x20, index);
            code[used++] = 0x10;
            used = WasmBinary.Unsigned(code, used, first + ReadUnitFunction);
            code[used++] = 0xfb; code[used++] = 0x0e; code[used++] = 4; // array.set Char[] (packed i16)
            used = Local(code, used, 0x20, index);
            used = Const(code, used, 1);
            code[used++] = 0x6a;
            used = Local(code, used, 0x21, index);
            code[used++] = 0x0c; code[used++] = 0;
            code[used++] = 0x0b; // loop
            code[used++] = 0x0b; // block
            used = Local(code, used, 0x20, characters);
            used = StringWasm.Wrap(code, used);
            used = Local(code, used, 0x21, result);
            code[used++] = 0x0b; // if
            return used;
        }

        internal static int WriteTypes(byte[] output, int cursor)
        {
            output[cursor++] = 0x60; output[cursor++] = 0; output[cursor++] = 1; output[cursor++] = 0x7f;
            output[cursor++] = 0x60; output[cursor++] = 1; output[cursor++] = 0x7f; output[cursor++] = 1; output[cursor++] = 0x7f;
            return cursor;
        }

        // module name, field name, kind and type index for readLine (8) and readUnit (8).
        internal static int ImportEntryBytes(int firstType) =>
            2 * (1 + ModuleNameLength + 1 + 8 + 1) + WasmBinary.UnsignedSize(firstType) + WasmBinary.UnsignedSize(firstType + 1);

        internal static int WriteEntries(byte[] output, int cursor, int firstType)
        {
            cursor = Name(output, cursor, "standardwasm:console");
            cursor = Name(output, cursor, "readLine");
            output[cursor++] = 0;
            cursor = WasmBinary.Unsigned(output, cursor, firstType);
            cursor = Name(output, cursor, "standardwasm:console");
            cursor = Name(output, cursor, "readUnit");
            output[cursor++] = 0;
            return WasmBinary.Unsigned(output, cursor, firstType + 1);
        }

        private static int Name(byte[] output, int cursor, string text)
        {
            output[cursor++] = (byte)text.Length;
            for (int i = 0; i < text.Length; i++) output[cursor++] = (byte)text[i];
            return cursor;
        }
    }
}
