using static PortableCompiler.BranchWasm;

namespace PortableCompiler
{
    internal static class InlineObjectStorageWasm
    {
        internal static int Allocate(byte[] code, int used, int vector, int local, int count)
        {
            used = Const(code, used, count);
            used = Gc(code, used, 7, vector); // array.new_default
            return Local(code, used, 0x21, local);
        }

        internal static int Clear(byte[] code, int used, int vector, int local, int count)
        {
            used = Local(code, used, 0x20, local);
            used = Const(code, used, 0);
            code[used++] = 0xd0; // ref.null eq
            code[used++] = 0x6d;
            used = Const(code, used, count);
            return Gc(code, used, 0x10, vector); // array.fill
        }

        // A value copy takes a snapshot. The stable owner remains private to
        // this frame slot and does not become the value copied elsewhere.
        internal static int Snapshot(byte[] code, int used, int type, int vector, int local, int count)
        {
            for (int cell = 0; cell < count; cell++)
            {
                used = Local(code, used, 0x20, local);
                used = Const(code, used, cell);
                used = Gc(code, used, 0x0b, vector); // array.get
            }
            return Gc(code, used, 0, type); // struct.new
        }

        // Copy fields into the owner's current storage so existing element
        // aliases observe the write, even when it sets the entire value.
        internal static int Store(byte[] code, int used, int type, int vector, int local, int source, int count)
        {
            used = Local(code, used, 0x20, source);
            code[used++] = 0xd1; // ref.is_null (default aggregate)
            code[used++] = 0x04;
            code[used++] = 0x40;
            used = Clear(code, used, vector, local, count);
            code[used++] = 0x05;
            for (int cell = 0; cell < count; cell++)
            {
                used = Local(code, used, 0x20, local);
                used = Const(code, used, cell);
                used = Local(code, used, 0x20, source);
                used = Gc(code, used, 2, type); // struct.get
                used = WasmBinary.Unsigned(code, used, cell);
                used = Gc(code, used, 0x0e, vector); // array.set
            }
            code[used++] = 0x0b;
            return used;
        }

        internal static int Set(byte[] code, int used, int vector, int local, int index, int value)
        {
            used = Local(code, used, 0x20, local);
            used = Const(code, used, index);
            used = Local(code, used, 0x20, value);
            return Gc(code, used, 0x0e, vector); // array.set
        }
    }
}
