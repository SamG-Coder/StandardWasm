using static PortableCompiler.BranchWasm;

namespace PortableCompiler
{
    internal static class CurrentThreadWasm
    {
        // Guest configuration for the single managed thread. The backing word is private
        // to the managed Thread runtime and has no meaning to Win32 or linear memory.
        private const int ManagedId = 1, NormalPriority = 2, BackingMarker = int.MinValue;
        internal const int Capacity = 160;
        internal const int RootKey = int.MinValue + 25;

        // Result is stored in a typed local to share allocation/identity with other admitted
        // CoreLib entry points. Desktop's QCall will then write it into its caller's slot.
        internal static int Load(byte[] code, int used, int type, int target, int root,
            int managedIdField, int priorityField, int backingField)
        {
            code[used++] = 0x23; // global.get
            used = WasmBinary.Unsigned(code, used, root);
            used = Local(code, used, 0x22, target);
            code[used++] = 0xd1; // ref.is_null
            code[used++] = 0x04; // if
            code[used++] = 0x40;
            used = Gc(code, used, 1, type); // struct.new_default
            used = Local(code, used, 0x21, target);
            used = InitializeField(code, used, target, type, managedIdField, ManagedId);
            used = InitializeField(code, used, target, type, priorityField, NormalPriority);
            used = InitializeField(code, used, target, type, backingField, BackingMarker);
            used = Local(code, used, 0x20, target);
            code[used++] = 0x24; // global.set
            used = WasmBinary.Unsigned(code, used, root);
            code[used++] = 0x0b;
            return used;
        }

        private static int InitializeField(byte[] code, int used, int target, int type, int field, int value)
        {
            used = Local(code, used, 0x20, target);
            used = Const(code, used, value);
            used = Gc(code, used, 5, type); // struct.set
            return WasmBinary.Unsigned(code, used, field);
        }
    }
}
