using static PortableCompiler.BranchWasm;

namespace PortableCompiler
{
    internal static class Utf16MemoryWasm
    {
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
