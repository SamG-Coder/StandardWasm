namespace PortableCompiler
{
    // Feature bits describe the import groups. The function index space
    // advances only for function imports.
    internal static class WasmImports
    {
        internal const int Console = 1, Resources = 2, Memory = 4, Win32Errors = 8;
        private const int FirstNativeFeature = 16;
        private const int NativeFeatures = ((1 << Win32ModuleBinding.Count) - 1) * FirstNativeFeature;

        internal static int Win32NativeFeature(int member) => FirstNativeFeature << (member - 1);

        internal const int Monitor = FirstNativeFeature << Win32ModuleBinding.Count;

        internal static int MonitorSlot(int features) => Win32NativeSlot(features, Win32ModuleBinding.Count + 1);

        internal static int FunctionCount(int features) => MonitorSlot(features) + ((features & Monitor) != 0 ? 1 : 0);

        internal static int Win32NativeSlot(int features, int member)
        {
            int slot = Win32ErrorBase(features) + ((features & Win32Errors) != 0 ? Win32ErrorWasm.FunctionCount : 0);
            for (int cursor = 1; cursor < member; cursor++)
                if ((features & Win32NativeFeature(cursor)) != 0) slot++;
            return slot;
        }

        internal static int Win32ErrorBase(int features) =>
            ConsoleFunctions(features) + ((features & Resources) != 0 ? ResourceWasm.FunctionCount : 0);

        internal static int TypeBytes(int features)
        {
            int size = ((features & Console) != 0 ? ConsoleWasm.TypeBytes : 0) +
                ((features & Resources) != 0 ? ResourceWasm.TypeBytes : 0) +
                ((features & Win32Errors) != 0 ? Win32ErrorWasm.TypeBytes : 0) +
                ((features & Monitor) != 0 ? MonitorWasm.TypeBytes : 0);
            for (int member = 1; member <= Win32ModuleBinding.Count; member++)
                if ((features & Win32NativeFeature(member)) != 0) size += Win32ModuleWasm.TypeBytes(member);
            return size;
        }

        private static int ConsoleFunctions(int features) => (features & Console) != 0 ? ConsoleWasm.ImportCount : 0;

        // Resource calls and native indirection share one memory index.
        private static bool HasMemory(int features) => (features & (Resources | Memory | NativeFeatures)) != 0;

        private static int EntryCount(int features) => FunctionCount(features) + (HasMemory(features) ? 1 : 0);

        internal static int Size(int features, int firstType)
        {
            int size = WasmBinary.UnsignedSize(EntryCount(features));
            if ((features & Console) != 0) size += ConsoleWasm.ImportEntryBytes(firstType);
            if (HasMemory(features)) size += LinearMemoryWasm.ImportBytes;
            if ((features & Resources) != 0) size += ResourceWasm.ImportEntryBytes(firstType + ConsoleFunctions(features));
            if ((features & Win32Errors) != 0) size += Win32ErrorWasm.ImportEntryBytes(firstType + Win32ErrorBase(features));
            for (int member = 1; member <= Win32ModuleBinding.Count; member++)
                if ((features & Win32NativeFeature(member)) != 0)
                    size += Win32ModuleWasm.ImportEntryBytes(firstType + Win32NativeSlot(features, member), member);
            if ((features & Monitor) != 0) size += MonitorWasm.EntryBytes(firstType + MonitorSlot(features));
            return size;
        }

        internal static int WriteTypes(byte[] output, int cursor, int features)
        {
            if ((features & Console) != 0) cursor = ConsoleWasm.WriteTypes(output, cursor);
            if ((features & Resources) != 0) cursor = ResourceWasm.WriteTypes(output, cursor);
            if ((features & Win32Errors) != 0) cursor = Win32ErrorWasm.WriteTypes(output, cursor);
            for (int member = 1; member <= Win32ModuleBinding.Count; member++)
                if ((features & Win32NativeFeature(member)) != 0)
                    cursor = Win32ModuleWasm.WriteType(output, cursor, member);
            if ((features & Monitor) != 0) cursor = MonitorWasm.WriteType(output, cursor);
            return cursor;
        }

        internal static int WriteEntries(byte[] output, int cursor, int features, int firstType)
        {
            cursor = WasmBinary.Unsigned(output, cursor, EntryCount(features));
            if ((features & Console) != 0) cursor = ConsoleWasm.WriteEntries(output, cursor, firstType);
            if (HasMemory(features)) cursor = LinearMemoryWasm.WriteImport(output, cursor);
            if ((features & Resources) != 0) cursor = ResourceWasm.WriteEntries(output, cursor, firstType + ConsoleFunctions(features));
            if ((features & Win32Errors) != 0) cursor = Win32ErrorWasm.WriteEntries(output, cursor, firstType + Win32ErrorBase(features));
            for (int member = 1; member <= Win32ModuleBinding.Count; member++)
                if ((features & Win32NativeFeature(member)) != 0)
                    cursor = Win32ModuleWasm.WriteEntry(output, cursor, firstType + Win32NativeSlot(features, member), member);
            if ((features & Monitor) != 0) cursor = MonitorWasm.WriteEntry(output, cursor, firstType + MonitorSlot(features));
            return cursor;
        }
    }
}
