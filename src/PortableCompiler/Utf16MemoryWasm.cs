using static PortableCompiler.BranchWasm;

namespace PortableCompiler
{
    internal static class Utf16MemoryWasm
    {
        internal const int PinCapacity = StackAllocationWasm.Capacity + 320;

        // Null remains address zero. A string already pinned by a live frame keeps its
        // address (PinRegistryWasm), as on the CLR. Otherwise it gets a frame-owned copy,
        // including a zero code unit even for an empty string.
        internal static int Pin(byte[] code, int used, int source, int chars, int length, int sizeOrIndex,
            int address, int sp, int stackBase, int stackLimit, int scratch, int pins)
        {
            used = Const(code, used, 0);
            used = Local(code, used, 0x21, address);
            used = Local(code, used, 0x20, source);
            code[used++] = 0xd1; // ref.is_null
            code[used++] = 0x45;
            code[used++] = 0x04; code[used++] = 0x40;
            used = PinRegistryWasm.Find(code, used, pins, source, sizeOrIndex, address);
            used = Local(code, used, 0x20, address);
            code[used++] = 0x45; // i32.eqz: not pinned yet
            code[used++] = 0x04; code[used++] = 0x40;
            used = Local(code, used, 0x20, source);
            used = StringWasm.Unwrap(code, used);
            used = Local(code, used, 0x22, chars);
            code[used++] = 0xfb; code[used++] = 0x0f; // array.len
            used = Local(code, used, 0x22, length);
            used = Const(code, used, StackAllocationWasm.Pages << 15);
            code[used++] = 0x4f; // i32.ge_u, leave room for the terminator
            code[used++] = 0x04; code[used++] = 0x40; code[used++] = 0x00; code[used++] = 0x0b;
            used = Local(code, used, 0x20, length);
            used = Const(code, used, 1);
            code[used++] = 0x6a;
            used = Const(code, used, 1);
            code[used++] = 0x74;
            used = Local(code, used, 0x21, sizeOrIndex);
            // Allocation zeroes the final code unit before Copy fills the contents.
            used = StackAllocationWasm.Allocate(code, used, sp, stackBase, stackLimit, sizeOrIndex, address, scratch);
            used = Copy(code, used, chars, length, address, sizeOrIndex);
            used = PinRegistryWasm.Register(code, used, pins, source, address);
            code[used++] = 0x0b; // not pinned
            code[used++] = 0x0b; // non-null
            return used;
        }

        // Copy exactly length UTF-16 code units from the traced character
        // vector into caller-owned contiguous memory. Allocation and any
        // trailing NUL are the caller's responsibility.
        internal static int Copy(byte[] code, int used, int chars, int length, int address, int index)
        {
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
            return used;
        }
    }
}
