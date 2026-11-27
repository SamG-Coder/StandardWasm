namespace PortableCompiler
{
    // Feature bits describe the import groups. The function index space
    // advances only for function imports.
    internal static class WasmImports
    {
        internal const long Console = 1, Resources = 2, Memory = 4, Win32Errors = 8;
        // A module with pointer-backed spans enables linear accesses in every
        // character-view consumer, including callees compiled before discovery.
        internal const long NativeCharacterViews = 1L << 60;
        private const long FirstNativeFeature = 16;
        private const long NativeMemoryFeatures = ((1L << Win32ModuleBinding.ReleaseContext) - 1) * FirstNativeFeature |
            (FirstNativeFeature << (Win32ModuleBinding.BCryptGenRandom - 1)) |
            (FirstNativeFeature << (Win32ModuleBinding.LoadLibrary - 1)) |
            (FirstNativeFeature << (Win32ModuleBinding.GetProcedure - 1)) |
            (FirstNativeFeature << (Win32ModuleBinding.RegisterClass - 1)) |
            (FirstNativeFeature << (Win32ModuleBinding.GetClassInfo - 1)) |
            (FirstNativeFeature << (Win32ModuleBinding.CreateWindow - 1)) |
            (FirstNativeFeature << (Win32ModuleBinding.GetFullPathName - 1)) |
            (FirstNativeFeature << (Win32ModuleBinding.RedrawWindow - 1)) |
            (FirstNativeFeature << (Win32ModuleBinding.RegisterWindowMessage - 1));

        internal static long Win32NativeFeature(int member) => FirstNativeFeature << (member - 1);

        internal const long Monitor = FirstNativeFeature << Win32ModuleBinding.Count;
        internal const long Debugger = Monitor << 1;
        internal const long ConsoleInput = Debugger << 1;

        internal static int MonitorSlot(long features) => Win32NativeSlot(features, Win32ModuleBinding.Count + 1);

        internal static int DebuggerSlot(long features) => MonitorSlot(features) + ((features & Monitor) != 0 ? 1 : 0);

        internal static int InputFunction(long features) => DebuggerSlot(features) + ((features & Debugger) != 0 ? 1 : 0);

        // Dynamic native entries follow every fixed group, so established host indices stay stable.
        internal static int FunctionCount(long features, NativeImportTable natives) =>
            FixedFunctionCount(features) + natives.Count;

        internal static int FixedFunctionCount(long features) => InputFunction(features) + ((features & ConsoleInput) != 0 ? ConsoleInputWasm.ImportCount : 0);

        internal static int Win32NativeSlot(long features, int member)
        {
            int slot = Win32ErrorBase(features) + ((features & Win32Errors) != 0 ? Win32ErrorWasm.FunctionCount : 0);
            for (int cursor = 1; cursor < member; cursor++)
                if ((features & Win32NativeFeature(cursor)) != 0) slot++;
            return slot;
        }

        internal static int Win32ErrorBase(long features) =>
            ConsoleFunctions(features) + ((features & Resources) != 0 ? ResourceWasm.FunctionCount : 0);

        internal static int TypeBytes(long features, NativeImportTable natives)
        {
            int size = ((features & Console) != 0 ? ConsoleWasm.TypeBytes : 0) +
                ((features & Resources) != 0 ? ResourceWasm.TypeBytes : 0) +
                ((features & Win32Errors) != 0 ? Win32ErrorWasm.TypeBytes : 0) +
                ((features & Monitor) != 0 ? MonitorWasm.TypeBytes : 0) +
                ((features & Debugger) != 0 ? RuntimeDebuggerWasm.TypeBytes : 0) +
                ((features & ConsoleInput) != 0 ? ConsoleInputWasm.TypeBytes : 0);
            for (int member = 1; member <= Win32ModuleBinding.Count; member++)
                if ((features & Win32NativeFeature(member)) != 0) size += Win32ModuleWasm.TypeBytes(member);
            return size + natives.TypeBytes();
        }

        private static int ConsoleFunctions(long features) => (features & Console) != 0 ? ConsoleWasm.ImportCount : 0;

        // Resource calls and native indirection share one memory index.
        private static bool HasMemory(long features) => (features & (Resources | Memory | NativeMemoryFeatures | NativeCharacterViews)) != 0;

        private static int EntryCount(long features, NativeImportTable natives) =>
            FunctionCount(features, natives) + (HasMemory(features) ? 1 : 0);

        // Whether the module has an import section at all.
        internal static bool Any(long features, NativeImportTable natives) => features != 0 || natives.Count != 0;

        internal static int Size(long features, int firstType, NativeImportTable natives)
        {
            int size = WasmBinary.UnsignedSize(EntryCount(features, natives));
            if ((features & Console) != 0) size += ConsoleWasm.ImportEntryBytes(firstType);
            if (HasMemory(features)) size += LinearMemoryWasm.ImportBytes;
            if ((features & Resources) != 0) size += ResourceWasm.ImportEntryBytes(firstType + ConsoleFunctions(features));
            if ((features & Win32Errors) != 0) size += Win32ErrorWasm.ImportEntryBytes(firstType + Win32ErrorBase(features));
            for (int member = 1; member <= Win32ModuleBinding.Count; member++)
                if ((features & Win32NativeFeature(member)) != 0)
                    size += Win32ModuleWasm.ImportEntryBytes(firstType + Win32NativeSlot(features, member), member);
            if ((features & Monitor) != 0) size += MonitorWasm.EntryBytes(firstType + MonitorSlot(features));
            if ((features & Debugger) != 0) size += RuntimeDebuggerWasm.EntryBytes(firstType + DebuggerSlot(features));
            if ((features & ConsoleInput) != 0) size += ConsoleInputWasm.ImportEntryBytes(firstType + InputFunction(features));
            size += natives.ImportEntryBytes(firstType + FixedFunctionCount(features));
            return size;
        }

        internal static int WriteTypes(byte[] output, int cursor, long features, NativeImportTable natives)
        {
            if ((features & Console) != 0) cursor = ConsoleWasm.WriteTypes(output, cursor);
            if ((features & Resources) != 0) cursor = ResourceWasm.WriteTypes(output, cursor);
            if ((features & Win32Errors) != 0) cursor = Win32ErrorWasm.WriteTypes(output, cursor);
            for (int member = 1; member <= Win32ModuleBinding.Count; member++)
                if ((features & Win32NativeFeature(member)) != 0)
                    cursor = Win32ModuleWasm.WriteType(output, cursor, member);
            if ((features & Monitor) != 0) cursor = MonitorWasm.WriteType(output, cursor);
            if ((features & Debugger) != 0) cursor = RuntimeDebuggerWasm.WriteType(output, cursor);
            if ((features & ConsoleInput) != 0) cursor = ConsoleInputWasm.WriteTypes(output, cursor);
            return natives.WriteTypes(output, cursor);
        }

        internal static int WriteEntries(byte[] output, int cursor, long features, int firstType, NativeImportTable natives)
        {
            cursor = WasmBinary.Unsigned(output, cursor, EntryCount(features, natives));
            if ((features & Console) != 0) cursor = ConsoleWasm.WriteEntries(output, cursor, firstType);
            if (HasMemory(features)) cursor = LinearMemoryWasm.WriteImport(output, cursor);
            if ((features & Resources) != 0) cursor = ResourceWasm.WriteEntries(output, cursor, firstType + ConsoleFunctions(features));
            if ((features & Win32Errors) != 0) cursor = Win32ErrorWasm.WriteEntries(output, cursor, firstType + Win32ErrorBase(features));
            for (int member = 1; member <= Win32ModuleBinding.Count; member++)
                if ((features & Win32NativeFeature(member)) != 0)
                    cursor = Win32ModuleWasm.WriteEntry(output, cursor, firstType + Win32NativeSlot(features, member), member);
            if ((features & Monitor) != 0) cursor = MonitorWasm.WriteEntry(output, cursor, firstType + MonitorSlot(features));
            if ((features & Debugger) != 0) cursor = RuntimeDebuggerWasm.WriteEntry(output, cursor, firstType + DebuggerSlot(features));
            if ((features & ConsoleInput) != 0) cursor = ConsoleInputWasm.WriteEntries(output, cursor, firstType + InputFunction(features));
            return natives.WriteEntries(output, cursor, firstType + FixedFunctionCount(features));
        }
    }
}
