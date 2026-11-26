using static PortableCompiler.BranchWasm;

namespace PortableCompiler
{
    // The native adapter lends stable offsets in the instance's wasm32 memory.
    // reserveName takes a UTF-16 code-unit count. locate takes the bundle owner,
    // name address and code-unit count, and returns length/address or a diagnostic.
    internal static class ResourceWasm
    {
        internal const int FunctionCount = 2, EntryCount = 3, TypeBytes = 12;
        private const string ModuleName = "standardwasm:resources";
        private const string Memory = "memory", Reserve = "reserveName", Locate = "locate";

        internal static int WriteTypes(byte[] output, int cursor)
        {
            output[cursor++] = 0x60;
            output[cursor++] = 1;
            output[cursor++] = 0x7f;
            output[cursor++] = 1;
            output[cursor++] = 0x7f;
            output[cursor++] = 0x60;
            output[cursor++] = 3;
            output[cursor++] = 0x7f;
            output[cursor++] = 0x7f;
            output[cursor++] = 0x7f;
            output[cursor++] = 1;
            output[cursor++] = 0x7e;
            return cursor;
        }

        internal static int ImportEntryBytes(int firstType) =>
            EntryCount * (1 + ModuleName.Length + 1 + 1) + Memory.Length + Reserve.Length + Locate.Length +
            2 + WasmBinary.UnsignedSize(firstType) + WasmBinary.UnsignedSize(firstType + 1);

        internal static int WriteEntries(byte[] output, int cursor, int firstType)
        {
            cursor = Entry(output, cursor, Memory, 2);
            output[cursor++] = 0; // wasm32 limits with no maximum
            output[cursor++] = 1; // one initial page
            cursor = Entry(output, cursor, Reserve, 0);
            cursor = WasmBinary.Unsigned(output, cursor, firstType);
            cursor = Entry(output, cursor, Locate, 0);
            return WasmBinary.Unsigned(output, cursor, firstType + 1);
        }

        // Copy the managed String into the host's scratch memory before the
        // native lookup. Null stays distinct from an allocated empty name.
        // The three i32 locals hold length, address and the copy index.
        internal static int Lookup(byte[] code, int used, int owner, int name, int chars, int scratch, int firstFunction)
        {
            int length = scratch, address = scratch + 1, index = scratch + 2;
            used = Const(code, used, 0);
            used = Local(code, used, 0x21, length);
            used = Const(code, used, 0);
            used = Local(code, used, 0x21, address);
            used = Local(code, used, 0x20, name);
            code[used++] = 0xd1; // ref.is_null
            code[used++] = 0x45; // i32.eqz
            code[used++] = 0x04; code[used++] = 0x40; // if present
            used = Local(code, used, 0x20, name);
            used = StringWasm.Unwrap(code, used);
            used = Local(code, used, 0x22, chars);
            code[used++] = 0xfb; code[used++] = 0x0f; // array.len
            used = Local(code, used, 0x22, length);
            code[used++] = 0x10; // call reserveName(length)
            used = WasmBinary.Unsigned(code, used, firstFunction);
            used = Local(code, used, 0x21, address);
            used = Const(code, used, 0);
            used = Local(code, used, 0x21, index);
            code[used++] = 0x02; code[used++] = 0x40; // copy block
            code[used++] = 0x03; code[used++] = 0x40; // copy loop
            used = Local(code, used, 0x20, index);
            used = Local(code, used, 0x20, length);
            code[used++] = 0x46; // i32.eq
            code[used++] = 0x0d; code[used++] = 1; // exit copy block

            used = Local(code, used, 0x20, address);
            used = Local(code, used, 0x20, index);
            used = Const(code, used, 1);
            code[used++] = 0x74; // i32.shl, two bytes per code unit
            code[used++] = 0x6a; // i32.add
            used = Local(code, used, 0x20, chars);
            used = Local(code, used, 0x20, index);
            code[used++] = 0xfb; code[used++] = 0x0d; code[used++] = 4; // array.get_u Char[]
            code[used++] = 0x3b; code[used++] = 1; code[used++] = 0; // i32.store16

            used = Local(code, used, 0x20, index);
            used = Const(code, used, 1);
            code[used++] = 0x6a;
            used = Local(code, used, 0x21, index);
            code[used++] = 0x0c; code[used++] = 0; // repeat copy loop
            code[used++] = 0x0b; // loop
            code[used++] = 0x0b; // block
            code[used++] = 0x0b; // if
            used = Local(code, used, 0x20, owner);
            used = Local(code, used, 0x20, address);
            used = Local(code, used, 0x20, length);
            code[used++] = 0x10; // call locate(owner, address, length)
            return WasmBinary.Unsigned(code, used, firstFunction + 1);
        }

        private static int Entry(byte[] output, int cursor, string name, int kind)
        {
            cursor = Ascii(output, cursor, ModuleName);
            cursor = Ascii(output, cursor, name);
            output[cursor++] = (byte)kind;
            return cursor;
        }

        private static int Ascii(byte[] output, int cursor, string name)
        {
            output[cursor++] = (byte)name.Length;
            for (int i = 0; i < name.Length; i++) output[cursor++] = (byte)name[i];
            return cursor;
        }
    }
}
