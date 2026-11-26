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
