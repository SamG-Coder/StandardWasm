using static PortableCompiler.BranchWasm;
using static PortableCompiler.SignatureTypes;

namespace PortableCompiler
{
    internal static class NativeByteTransferWasm
    {
        private const int Source = 0, Destination = 1, Start = 2, Length = 3, Capacity = 4, Index = 5;

        internal static int Write(byte[] code, int used, int function, int[] bodies, int[] sizes)
        {
            if (function < 0) return used;
            if (code.Length - used < 192) return -3;
            bodies[function] = used;
            code[used++] = 1; code[used++] = 2; code[used++] = 0x7f;

            used = Get(code, used, Destination);
            code[used++] = 0xfb; code[used++] = 0x0f; // array.len
            used = Local(code, used, 0x21, Capacity);
            used = Get(code, Get(code, used, Start), Capacity);
            code[used++] = 0x4b; // start > capacity (unsigned)
            used = Get(code, Get(code, Get(code, used, Length), Capacity), Start);
            code[used++] = 0x6b;
            code[used++] = 0x4b; // length > capacity - start (unsigned)
            code[used++] = 0x72;
            used = ExceptionTagsWasm.ThrowIf(code, used, ExceptionTagsWasm.ArgumentOutOfRangeTag);
            used = Get(code, used, Length);
            code[used++] = 0x45;
            code[used++] = 0x04; code[used++] = 0x40;
            code[used++] = 0x0f; code[used++] = 0x0b;

            // Check the entire source before writing any destination byte.
            // A 64-bit bound also represents a full 4 GiB wasm32 memory.
            used = Get(code, used, Source); code[used++] = 0xad;
            used = Get(code, used, Length); code[used++] = 0xad;
            code[used++] = 0x7c; // i64.add
            code[used++] = 0x3f; code[used++] = 0;
            code[used++] = 0xad;
            code[used++] = 0x42; code[used++] = 16;
            code[used++] = 0x86; // i64.shl
            code[used++] = 0x56; // i64.gt_u
            code[used++] = 0x04; code[used++] = 0x40;
            code[used++] = 0x00; code[used++] = 0x0b;

            code[used++] = 0x03; code[used++] = 0x40;
            used = Get(code, Get(code, Get(code, used, Destination), Start), Index);
            code[used++] = 0x6a;
            used = Get(code, Get(code, used, Source), Index);
            code[used++] = 0x6a;
            used = LinearMemoryWasm.Load(code, used, 0x47);
            used = Gc(code, used, 14, Bytes); // array.set
            used = Get(code, used, Index);
            used = Const(code, used, 1);
            code[used++] = 0x6a;
            used = Local(code, used, 0x22, Index);
            used = Get(code, used, Length);
            code[used++] = 0x49; // i32.lt_u
            code[used++] = 0x0d; code[used++] = 0;
            code[used++] = 0x0b;
            code[used++] = 0x0b;
            sizes[function] = used - bodies[function];
            return used;
        }

        private static int Get(byte[] code, int used, int index) => Local(code, used, 0x20, index);
    }
}
