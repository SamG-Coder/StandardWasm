using static PortableCompiler.BranchWasm;
using static PortableCompiler.SignatureTypes;

namespace PortableCompiler
{
    internal static class AsciiCaseWasm
    {
        internal const int BodyCapacity = 256;
        private const int Input = 0, Length = 1, Index = 2, Unit = 3, Original = 4, Changed = 5;

        internal static int Lowercase(byte[] code, int used, int empty)
        {
            code[used++] = 2;
            code[used++] = 3; code[used++] = 0x7f;
            code[used++] = 2; code[used++] = 0x63; // nullable char[]
            used = WasmBinary.Unsigned(code, used, WasmBinary.HeapType(Chars));
            used = Local(code, used, 0x20, Input);
            code[used++] = 0xd1; // ref.is_null
            used = ExceptionTagsWasm.ThrowIf(code, used, ExceptionTagsWasm.NullTag);
            used = StringWasm.Unwrap(code, Local(code, used, 0x20, Input));
            used = Local(code, used, 0x22, Original);
            code[used++] = 0xfb; code[used++] = 0x0f; // array.len
            used = Local(code, used, 0x22, Length);
            code[used++] = 0x45;
            used = Open(code, used, 0x04);
            used = StringWasm.LoadLiteral(code, used, empty, 0, 0);
            code[used++] = 0x0f; code[used++] = 0x0b;

            // Look for the first uppercase code unit and retain the original
            // string if the loop finishes without finding one.
            used = Open(code, used, 0x02);
            used = Open(code, used, 0x03);
            used = IsEnd(code, used);
            used = Open(code, used, 0x04);
            used = Local(code, used, 0x20, Input);
            code[used++] = 0x0f; code[used++] = 0x0b;
            used = IsUppercase(code, LoadUnit(code, used));
            code[used++] = 0x0d; code[used++] = 1; // br_if result block
            used = Advance(code, used);
            code[used++] = 0x0c; code[used++] = 0;
            code[used++] = 0x0b;
            code[used++] = 0x0b;

            // Initialise the fresh array's unchanged prefix, then write the
            // rest one code unit at a time.
            used = Local(code, used, 0x20, Length);
            used = Gc(code, used, 7, Chars);
            used = Local(code, used, 0x22, Changed);
            used = Const(code, used, 0);
            used = Local(code, used, 0x20, Original);
            used = Const(code, used, 0);
            used = Local(code, used, 0x20, Index);
            used = Gc(code, used, 0x11, Chars);
            used = WasmBinary.Unsigned(code, used, WasmBinary.HeapType(Chars));

            used = Open(code, used, 0x02);
            used = Open(code, used, 0x03);
            used = IsEnd(code, used);
            code[used++] = 0x0d; code[used++] = 1;
            used = Local(code, LoadUnit(code, used), 0x21, Unit);
            used = Local(code, used, 0x20, Changed);
            used = Local(code, used, 0x20, Index);
            used = Local(code, used, 0x20, Unit);
            used = Const(code, used, 0x20);
            code[used++] = 0x72; // i32.or
            used = Local(code, used, 0x20, Unit);
            used = IsUppercase(code, Local(code, used, 0x20, Unit));
            code[used++] = 0x1b; // select
            used = Gc(code, used, 14, Chars); // array.set
            used = Advance(code, used);
            code[used++] = 0x0c; code[used++] = 0;
            code[used++] = 0x0b;
            code[used++] = 0x0b;
            used = StringWasm.Wrap(code, Local(code, used, 0x20, Changed));
            code[used++] = 0x0b;
            return used;
        }

        private static int Open(byte[] code, int used, byte operation)
        {
            code[used++] = operation; code[used++] = 0x40;
            return used;
        }

        private static int LoadUnit(byte[] code, int used)
        {
            used = Local(code, used, 0x20, Original);
            used = Local(code, used, 0x20, Index);
            return Gc(code, used, 0x0d, Chars); // array.get_u
        }

        private static int IsEnd(byte[] code, int used)
        {
            used = Local(code, used, 0x20, Index);
            used = Local(code, used, 0x20, Length);
            code[used++] = 0x46;
            return used;
        }

        private static int Advance(byte[] code, int used)
        {
            used = Local(code, used, 0x20, Index);
            used = Const(code, used, 1);
            code[used++] = 0x6a;
            return Local(code, used, 0x21, Index);
        }

        private static int IsUppercase(byte[] code, int used)
        {
            used = Const(code, used, 'A');
            code[used++] = 0x6b;
            used = Const(code, used, 26);
            code[used++] = 0x49; // unsigned in-range check
            return used;
        }
    }
}
