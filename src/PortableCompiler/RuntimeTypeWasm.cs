namespace PortableCompiler
{
    internal static class RuntimeTypeWasm
    {
        internal const int DescriptorBytes = 6;

        internal static bool HasDescriptors(int[] globals)
        {
            for (int i = 0; i < globals[0]; i++)
                if (RuntimeTypeTokens.IsDescriptorOwner(StaticFields.Owner(globals, i))) return true;
            return false;
        }

        internal static int WriteDescriptor(byte[] output, int cursor)
        {
            output[cursor++] = 0x5f; // struct
            output[cursor++] = 2;
            output[cursor++] = 0x7f; // defining module slot
            output[cursor++] = 0;
            output[cursor++] = 0x7f; // TypeDef row or closed layout key
            output[cursor++] = 0;
            return cursor;
        }

        internal static int InitializerSize(int heap, int module, int row) =>
            4 + WasmBinary.UnsignedSize(module * 2) + WasmBinary.UnsignedSize(row * 2) + WasmBinary.UnsignedSize(heap);

        internal static int WriteInitializer(byte[] output, int cursor, int heap, int module, int row)
        {
            output[cursor++] = 0x41;
            cursor = WasmBinary.Signed(output, cursor, module);
            output[cursor++] = 0x41;
            cursor = WasmBinary.Signed(output, cursor, row);
            // The immutable global allocates the descriptor once per type and module instance.
            output[cursor++] = 0xfb;
            output[cursor++] = 0; // struct.new
            return WasmBinary.Unsigned(output, cursor, heap);
        }
    }
}
