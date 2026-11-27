using static PortableCompiler.BranchWasm;
using static PortableCompiler.SignatureTypes;

namespace PortableCompiler
{
    internal static class RuntimeTerminationWasm
    {
        internal const int BodyCapacity = 256;
        private const int CharacterArray = 3, Index = 4;
        private const string Module = "standardwasm:fatal";

        internal static int RegisterWrite(NativeImportTable imports) =>
            Register(imports, -1, "write", new byte[] { 0x7f, 0x7f });

        internal static int RegisterTerminate(NativeImportTable imports) =>
            Register(imports, -2, "terminate", new byte[] { 0x7f, 0x6d });

        private static int Register(NativeImportTable imports, int id, string member, byte[] types)
        {
            byte[] module = NativeImportTable.Ascii(Module);
            byte[] name = NativeImportTable.Ascii(member);
            return imports.Add(-2, id, module, module.Length, name, name.Length, types, types.Length, 0);
        }

        internal static int WriteBody(byte[] code, int used, int write, int terminate, int initializer)
        {
            code[used++] = 2; // One Char[] local followed by one i32 index.
            code[used++] = 1;
            used = WasmBinary.ValueType(code, used, Chars);
            code[used++] = 1;
            code[used++] = 0x7f;
            if (initializer >= 0) used = CallImport(code, used, initializer);

            used = WriteText(code, used, 0, 0, write);
            used = WriteText(code, used, 2, 1, write);

            // A presence mask preserves null separately from empty. The exception
            // crosses as an opaque, nullable managed reference.
            used = Present(code, used, 2);
            used = Const(code, used, 1);
            code[used++] = 0x74; // i32.shl
            used = Present(code, used, 0);
            code[used++] = 0x72; // i32.or
            used = Local(code, used, 0x20, 1);
            used = CallImport(code, used, terminate);
            // A Wasm trap cannot be intercepted by managed catch/finally.
            // The application host also retires this instance at the terminal import.
            code[used++] = 0x00;
            code[used++] = 0x0b;
            return used;
        }

        private static int Present(byte[] code, int used, int text)
        {
            used = Local(code, used, 0x20, text);
            code[used++] = 0xd1; // ref.is_null
            code[used++] = 0x45; // i32.eqz
            return used;
        }

        private static int WriteText(byte[] code, int used, int text, int field, int import)
        {
            used = Present(code, used, text);
            code[used++] = 0x04;
            code[used++] = 0x40;
            used = Local(code, used, 0x20, text);
            used = StringWasm.Unwrap(code, used);
            used = Local(code, used, 0x21, CharacterArray);
            used = Const(code, used, 0);
            used = Local(code, used, 0x21, Index);
            code[used++] = 0x02;
            code[used++] = 0x40; // block: finish at the string length
            code[used++] = 0x03;
            code[used++] = 0x40; // loop over UTF-16 code units
            used = Local(code, used, 0x20, Index);
            used = Local(code, used, 0x20, CharacterArray);
            code[used++] = 0xfb;
            code[used++] = 0x0f; // array.len
            code[used++] = 0x4f; // i32.ge_u
            code[used++] = 0x0d;
            code[used++] = 1;
            used = Const(code, used, field);
            used = Local(code, used, 0x20, CharacterArray);
            used = Local(code, used, 0x20, Index);
            code[used++] = 0xfb;
            code[used++] = 0x0d; // array.get_u
            code[used++] = 4; // Char[]
            used = CallImport(code, used, import);
            used = Local(code, used, 0x20, Index);
            used = Const(code, used, 1);
            code[used++] = 0x6a; // i32.add
            used = Local(code, used, 0x21, Index);
            code[used++] = 0x0c;
            code[used++] = 0;
            code[used++] = 0x0b;
            code[used++] = 0x0b;
            code[used++] = 0x0b;
            return used;
        }

        private static int CallImport(byte[] code, int used, int target)
        {
            code[used++] = 0x10;
            return WasmBinary.Unsigned(code, used, target);
        }
    }
}
