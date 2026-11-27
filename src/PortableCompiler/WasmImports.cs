namespace PortableCompiler
{
    // Feature bits describe the import groups. The function index space
    // advances only for function imports.
    internal static class WasmImports
    {
        // ConsoleInput (Stage 151, decision 0079) follows the resource functions, so the
        // indices of earlier groups never move.
        internal const int Console = 1, Resources = 2, Memory = 4, ConsoleInput = 8;

        // Stage 156 (decision 0083): the reached native imports follow every fixed group, one
        // function type each, so the fixed groups' indices never move.
        internal static int FunctionCount(int features, NativeImportTable natives) =>
            FixedFunctionCount(features) + natives.Count;

        // The function index of native entry 0.
        internal static int FixedFunctionCount(int features) =>
            InputFunction(features) + ((features & ConsoleInput) != 0 ? ConsoleInputWasm.ImportCount : 0);

        // The function index of readLine, the first ConsoleInput import.
        internal static int InputFunction(int features) =>
            ConsoleFunctions(features) + ((features & Resources) != 0 ? ResourceWasm.FunctionCount : 0);

        internal static int TypeBytes(int features, NativeImportTable natives) =>
            ((features & Console) != 0 ? ConsoleWasm.TypeBytes : 0) +
            ((features & Resources) != 0 ? ResourceWasm.TypeBytes : 0) +
            ((features & ConsoleInput) != 0 ? ConsoleInputWasm.TypeBytes : 0) + natives.TypeBytes();

        private static int ConsoleFunctions(int features) => (features & Console) != 0 ? ConsoleWasm.ImportCount : 0;

        // Resource calls and native indirection share one memory index.
        private static bool HasMemory(int features) => (features & (Resources | Memory)) != 0;

        private static int EntryCount(int features, NativeImportTable natives) =>
            FunctionCount(features, natives) + (HasMemory(features) ? 1 : 0);

        // Whether the module has an import section at all.
        internal static bool Any(int features, NativeImportTable natives) => features != 0 || natives.Count != 0;

        internal static int Size(int features, int firstType, NativeImportTable natives)
        {
            int size = WasmBinary.UnsignedSize(EntryCount(features, natives));
            if ((features & Console) != 0) size += ConsoleWasm.ImportEntryBytes(firstType);
            if (HasMemory(features)) size += LinearMemoryWasm.ImportBytes;
            if ((features & Resources) != 0) size += ResourceWasm.ImportEntryBytes(firstType + ConsoleFunctions(features));
            if ((features & ConsoleInput) != 0) size += ConsoleInputWasm.ImportEntryBytes(firstType + InputFunction(features));
            size += natives.ImportEntryBytes(firstType + FixedFunctionCount(features));
            return size;
        }

        internal static int WriteTypes(byte[] output, int cursor, int features, NativeImportTable natives)
        {
            if ((features & Console) != 0) cursor = ConsoleWasm.WriteTypes(output, cursor);
            if ((features & Resources) != 0) cursor = ResourceWasm.WriteTypes(output, cursor);
            if ((features & ConsoleInput) != 0) cursor = ConsoleInputWasm.WriteTypes(output, cursor);
            return natives.WriteTypes(output, cursor);
        }

        internal static int WriteEntries(byte[] output, int cursor, int features, int firstType, NativeImportTable natives)
        {
            cursor = WasmBinary.Unsigned(output, cursor, EntryCount(features, natives));
            if ((features & Console) != 0) cursor = ConsoleWasm.WriteEntries(output, cursor, firstType);
            if (HasMemory(features)) cursor = LinearMemoryWasm.WriteImport(output, cursor);
            if ((features & Resources) != 0) cursor = ResourceWasm.WriteEntries(output, cursor, firstType + ConsoleFunctions(features));
            if ((features & ConsoleInput) != 0)
                cursor = ConsoleInputWasm.WriteEntries(output, cursor, firstType + InputFunction(features));
            return natives.WriteEntries(output, cursor, firstType + FixedFunctionCount(features));
        }
    }
}
