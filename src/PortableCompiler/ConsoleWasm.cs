using PeLibrary;
using static PortableCompiler.BranchWasm;

namespace PortableCompiler
{
    // Lowers Console output to the host contract in decision 0037:
    //   standardwasm:console.write(stream i32, codeUnit i32) -> ()
    //   standardwasm:console.newLine(stream i32) -> ()
    // Stream 1 is standard output and stream 2 is standard error. The module
    // passes UTF-16 code units unchanged. The host owns Console.OutputEncoding
    // and Environment.NewLine, as the operating system does for the CLR.
    internal static class ConsoleWasm
    {
        internal const int ImportCount = 2, WriteFunction = 0, NewLineFunction = 1;
        // Loop, guards and two calls for one Write or WriteLine call site.
        internal const int WriteCapacity = 96;

        // Module "standardwasm:console", two function imports, two type indices.
        private const int ModuleNameLength = 20;

        internal static int Stream(int accessor) =>
            accessor == ConsoleMetadata.StandardOutput ? 1 : 2;

        // Stack: [writer, text] -> [] or [writer] -> [] for WriteLine().
        // CLR TextWriter.Write(null) writes nothing and WriteLine(null) writes
        // only the line terminator, so a null String is not an exception here.
        internal static int Write(byte[] code, int used, int member, int writer, int text,
            int characters, int index, int length)
        {
            // [writer] -> [] for Flush(). The CLR console writers auto-flush, and the host
            // receives every code unit synchronously, so nothing is buffered in the module.
            if (member == ConsoleMetadata.Flush) return used;
            if (member != ConsoleMetadata.WriteEmptyLine)
            {
                used = Local(code, used, 0x20, text);
                code[used++] = 0xd1; // ref.is_null
                code[used++] = 0x45; // i32.eqz
                code[used++] = 0x04;
                code[used++] = 0x40; // if the String is present
                used = Local(code, used, 0x20, text);
                used = StringWasm.Unwrap(code, used);
                used = Local(code, used, 0x22, characters);
                code[used++] = 0xfb;
                code[used++] = 0x0f; // array.len
                used = Local(code, used, 0x21, length);
                used = Const(code, used, 0);
                used = Local(code, used, 0x21, index);
                code[used++] = 0x02;
                code[used++] = 0x40; // block: exit after the last code unit
                code[used++] = 0x03;
                code[used++] = 0x40; // loop over code units in order
                used = Local(code, used, 0x20, index);
                used = Local(code, used, 0x20, length);
                code[used++] = 0x4f; // i32.ge_u
                code[used++] = 0x0d;
                code[used++] = 1; // br_if block
                used = Local(code, used, 0x20, writer);
                used = Local(code, used, 0x20, characters);
                used = Local(code, used, 0x20, index);
                code[used++] = 0xfb;
                code[used++] = 0x0d; // array.get_u Char[]: a code unit, not a code point
                code[used++] = 4;
                code[used++] = 0x10;
                code[used++] = WriteFunction;
                used = Local(code, used, 0x20, index);
                used = Const(code, used, 1);
                code[used++] = 0x6a; // i32.add
                used = Local(code, used, 0x21, index);
                code[used++] = 0x0c;
                code[used++] = 0; // br loop
                code[used++] = 0x0b;
                code[used++] = 0x0b;
                code[used++] = 0x0b;
            }
            if (member != ConsoleMetadata.Write)
            {
                used = Local(code, used, 0x20, writer);
                code[used++] = 0x10;
                code[used++] = NewLineFunction;
            }
            return used;
        }

        internal const int TypeBytes = 5 + 4; // (i32, i32) -> () and (i32) -> ()

        internal static int WriteTypes(byte[] output, int cursor)
        {
            output[cursor++] = 0x60;
            output[cursor++] = 2;
            output[cursor++] = 0x7f;
            output[cursor++] = 0x7f;
            output[cursor++] = 0;
            output[cursor++] = 0x60;
            output[cursor++] = 1;
            output[cursor++] = 0x7f;
            output[cursor++] = 0;
            return cursor;
        }

        internal static int ImportEntryBytes(int firstType) =>
            2 * (1 + ModuleNameLength + 1 + 1) + 5 + 7 +
            WasmBinary.UnsignedSize(firstType) + WasmBinary.UnsignedSize(firstType + 1);

        internal static int WriteEntries(byte[] output, int cursor, int firstType)
        {
            cursor = Import(output, cursor);
            output[cursor++] = 5;
            output[cursor++] = 0x77; // write
            output[cursor++] = 0x72;
            output[cursor++] = 0x69;
            output[cursor++] = 0x74;
            output[cursor++] = 0x65;
            output[cursor++] = 0;
            cursor = WasmBinary.Unsigned(output, cursor, firstType);
            cursor = Import(output, cursor);
            output[cursor++] = 7;
            output[cursor++] = 0x6e; // newLine
            output[cursor++] = 0x65;
            output[cursor++] = 0x77;
            output[cursor++] = 0x4c;
            output[cursor++] = 0x69;
            output[cursor++] = 0x6e;
            output[cursor++] = 0x65;
            output[cursor++] = 0;
            return WasmBinary.Unsigned(output, cursor, firstType + 1);
        }

        private static int Import(byte[] output, int cursor)
        {
            // "standardwasm:console"
            output[cursor++] = ModuleNameLength;
            cursor = Word(output, cursor, 0x6e617473);
            cursor = Word(output, cursor, 0x64726164);
            cursor = Word(output, cursor, 0x6d736177);
            cursor = Word(output, cursor, 0x6e6f633a);
            return Word(output, cursor, 0x656c6f73);
        }

        // Four ASCII bytes packed little-endian.
        private static int Word(byte[] output, int cursor, uint word)
        {
            for (int shift = 0; shift < 32; shift += 8)
                output[cursor++] = unchecked((byte)(word >> shift));
            return cursor;
        }
    }
}
