namespace PortableCompiler
{
    // Feature bits describe the import groups. The function index space
    // advances only for function imports.
    internal static class WasmImports
    {
        internal const int Console = 1, Resources = 2, Memory = 4;

        internal static int FunctionCount(int features) =>
            ConsoleFunctions(features) + ((features & Resources) != 0 ? ResourceWasm.FunctionCount : 0);

        internal static int TypeBytes(int features) =>
            ((features & Console) != 0 ? ConsoleWasm.TypeBytes : 0) +
            ((features & Resources) != 0 ? ResourceWasm.TypeBytes : 0);

        private static int ConsoleFunctions(int features) => (features & Console) != 0 ? ConsoleWasm.ImportCount : 0;

        // Resource calls and native indirection share one memory index.
        private static bool HasMemory(int features) => (features & (Resources | Memory)) != 0;

        private static int EntryCount(int features) => FunctionCount(features) + (HasMemory(features) ? 1 : 0);

        internal static int Size(int features, int firstType)
        {
            int size = WasmBinary.UnsignedSize(EntryCount(features));
            if ((features & Console) != 0) size += ConsoleWasm.ImportEntryBytes(firstType);
            if (HasMemory(features)) size += LinearMemoryWasm.ImportBytes;
            if ((features & Resources) != 0) size += ResourceWasm.ImportEntryBytes(firstType + ConsoleFunctions(features));
            return size;
        }

        internal static int WriteTypes(byte[] output, int cursor, int features)
        {
            if ((features & Console) != 0) cursor = ConsoleWasm.WriteTypes(output, cursor);
            if ((features & Resources) != 0) cursor = ResourceWasm.WriteTypes(output, cursor);
            return cursor;
        }

        internal static int WriteEntries(byte[] output, int cursor, int features, int firstType)
        {
            cursor = WasmBinary.Unsigned(output, cursor, EntryCount(features));
            if ((features & Console) != 0) cursor = ConsoleWasm.WriteEntries(output, cursor, firstType);
            if (HasMemory(features)) cursor = LinearMemoryWasm.WriteImport(output, cursor);
            if ((features & Resources) != 0) cursor = ResourceWasm.WriteEntries(output, cursor, firstType + ConsoleFunctions(features));
            return cursor;
        }
    }
}
