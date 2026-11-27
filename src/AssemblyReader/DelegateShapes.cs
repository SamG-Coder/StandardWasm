namespace PeLibrary
{
    // Stage 204 (issue #256, decision 0091): closed delegate types reached by the program, generic by
    // delegate type and Invoke signature. Each entry has a storage key in [KeyBase, KeyBase +
    // MaxDelegates), below FirstClass, so no code mistakes a delegate for a class layout. Every entry
    // shares the heap-3 closure (eqref target, funcref method). Its Invoke signature gives the thunk
    // function type the module appends after its fixed types.
    //
    // Entries live in the layout registry's auxiliary area (growing inward from the end, like enum
    // identities), linked from ObjectLayouts.DelegateHead:
    //   [next, ordinal, kind, assembly, row, result, helper, count, nominal count,
    //    parameter storages..., nominal arguments...]
    // helper is the invoke helper's function index + 1 for the current pass, or 0.
    // A platform delegate (System.Func`n, System.Action`n) is identified by kind, arity and its
    // nominal generic arguments, so the same instantiation from any assembly is one entry. Nominal,
    // not storage: Action<int> and Action<uint> share an Invoke shape but are distinct types, and a
    // closed generic over them (Slot<Action<int>>) must not alias (Codex review of f6e0ba1). A
    // delegate TypeDef is identified by assembly and row and has no nominal arguments.
    internal static class DelegateShapes
    {
        internal const int KeyBase = 0x300, MaxDelegates = 256;
        internal const int PlatformFunc = 1, PlatformAction = 2;
        private const int Next = 0, Ordinal = 1, Kind = 2, Assembly = 3, Row = 4, Result = 5, Helper = 6, Count = 7, NominalCount = 8, Parameters = 9;

        internal static bool IsKey(int type) => type >= KeyBase && type < KeyBase + MaxDelegates;

        // The storage key for the delegate type (kind, assembly, row, nominal[0..nominalCount)), whose
        // Invoke shape is (parameters[0..count)) -> result, registering it on first use. The identity
        // determines the shape, so only the identity is compared. -3 when the registry is full, -2 past
        // MaxDelegates.
        internal static int Register(int[] registry, int kind, int assembly, int row, int result, int[] parameters, int count,
            int[] nominal, int nominalCount)
        {
            int ordinals = 0;
            for (int entry = registry[ObjectLayouts.DelegateHead]; entry != 0; entry = registry[entry + Next])
            {
                ordinals++;
                if (registry[entry + Kind] != kind || registry[entry + Assembly] != assembly || registry[entry + Row] != row ||
                    registry[entry + NominalCount] != nominalCount) continue;
                int first = entry + Parameters + registry[entry + Count];
                bool same = true;
                for (int i = 0; i < nominalCount && same; i++) same = registry[first + i] == nominal[i];
                if (same) return KeyBase + registry[entry + Ordinal];
            }
            if (ordinals >= MaxDelegates) return -2;
            int size = Parameters + count + nominalCount;
            int start = registry.Length - registry[2] - size;
            if (start < ObjectLayouts.FieldMapStart + registry[1]) return -3;
            registry[start + Next] = registry[ObjectLayouts.DelegateHead];
            registry[start + Ordinal] = ordinals;
            registry[start + Kind] = kind;
            registry[start + Assembly] = assembly;
            registry[start + Row] = row;
            registry[start + Result] = result;
            registry[start + Helper] = 0;
            registry[start + Count] = count;
            registry[start + NominalCount] = nominalCount;
            for (int i = 0; i < count; i++) registry[start + Parameters + i] = parameters[i];
            for (int i = 0; i < nominalCount; i++) registry[start + Parameters + count + i] = nominal[i];
            registry[ObjectLayouts.DelegateHead] = start;
            registry[2] = registry[2] + size;
            return KeyBase + ordinals;
        }

        internal static int EntryCount(int[] registry)
        {
            int count = 0;
            for (int entry = registry[ObjectLayouts.DelegateHead]; entry != 0; entry = registry[entry + Next]) count++;
            return count;
        }

        private static int Entry(int[] registry, int key)
        {
            int ordinal = key - KeyBase;
            for (int entry = registry[ObjectLayouts.DelegateHead]; entry != 0; entry = registry[entry + Next])
                if (registry[entry + Ordinal] == ordinal) return entry;
            return 0;
        }

        // The Invoke result storage (Void = 0 for an Action), or -1 for an unknown key.
        internal static int InvokeResult(int[] registry, int key)
        {
            int entry = Entry(registry, key);
            return entry == 0 ? -1 : registry[entry + Result];
        }

        internal static int InvokeParameterCount(int[] registry, int key)
        {
            int entry = Entry(registry, key);
            return entry == 0 ? -1 : registry[entry + Count];
        }

        internal static int Family(int[] registry, int key)
        {
            int entry = Entry(registry, key);
            return entry == 0 ? 0 : registry[entry + Kind];
        }

        internal static int NominalArgument(int[] registry, int key, int index)
        {
            int entry = Entry(registry, key);
            return registry[entry + Parameters + registry[entry + Count] + index];
        }

        internal static int InvokeParameter(int[] registry, int key, int index) =>
            registry[Entry(registry, key) + Parameters + index];

        // The invoke helper's function index, or -1 before one is registered.
        internal static int InvokeHelper(int[] registry, int key) => registry[Entry(registry, key) + Helper] - 1;

        internal static void SetInvokeHelper(int[] registry, int key, int function) =>
            registry[Entry(registry, key) + Helper] = function + 1;

        // The key of the entry with this ordinal (0 .. EntryCount - 1).
        internal static int KeyAt(int ordinal) => KeyBase + ordinal;
    }
}
