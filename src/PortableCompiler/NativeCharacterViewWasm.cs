using static PortableCompiler.BranchWasm;
using static PortableCompiler.SignatureTypes;

namespace PortableCompiler
{
    // A null owner on a non-null view or location denotes linear memory.
    // The offset then holds a byte address instead of an array index.
    internal static class NativeCharacterViewWasm
    {
        internal static int Create(byte[] code, int used, int pointer, int length)
        {
            used = Local(code, used, 0x20, length);
            used = Const(code, used, 0);
            code[used++] = 0x48; // i32.lt_s
            used = ExceptionTagsWasm.ThrowIf(code, used, ExceptionTagsWasm.ArgumentOutOfRangeTag);
            used = NullOwner(code, used);
            used = Local(code, used, 0x20, pointer);
            used = Local(code, used, 0x20, length);
            return Gc(code, used, 0, WritableCharacterSpan);
        }

        internal static int ElementLocation(byte[] code, int used, int view, int index)
        {
            used = OpenAccess(code, used, view, index, CharacterLocation);
            used = NullOwner(code, used);
            used = LinearAddress(code, used, view, index);
            used = Gc(code, used, 0, CharacterLocation);
            code[used++] = 0x05; // else: owning array and element index
            used = SpanWasm.CheckedArrayAddress(code, used, Chars, view, index);
            used = Gc(code, used, 0, CharacterLocation);
            code[used++] = 0x0b;
            return used;
        }

        internal static int Read(byte[] code, int used, int view, int index)
        {
            used = OpenAccess(code, used, view, index, I32);
            // Reuse the consumed index local to hold the address across the null check.
            used = LinearAddress(code, used, view, index);
            used = Local(code, used, 0x21, index);
            used = LinearMemoryWasm.GuardNull(code, used, index);
            used = LinearMemoryWasm.Load(code, Local(code, used, 0x20, index), 0x49);
            code[used++] = 0x05;
            used = SpanWasm.CheckedArrayAddress(code, used, Chars, view, index);
            used = Gc(code, used, 0x0d, Chars);
            code[used++] = 0x0b;
            return used;
        }

        internal static int GuardBase(byte[] code, int used, int view)
        {
            used = SpanWasm.LoadField(code, used, view, 1);
            code[used++] = 0x45;
            return ExceptionTagsWasm.ThrowIf(code, used, ExceptionTagsWasm.NullTag);
        }

        internal static int GuardExtent(byte[] code, int used, int view, int length)
        {
            // Compare the exclusive end in i64 so neither a wasm32 address
            // rollover nor memory.size == 65,536 pages can wrap a bound.
            used = SpanWasm.LoadField(code, used, view, 1);
            code[used++] = 0xad; // i64.extend_i32_u
            used = Local(code, used, 0x20, length);
            code[used++] = 0xad;
            code[used++] = 0x42; code[used++] = 1;
            code[used++] = 0x86; // i64.shl
            code[used++] = 0x7c; // i64.add
            code[used++] = 0x3f; code[used++] = 0; // memory.size 0
            code[used++] = 0xad;
            code[used++] = 0x42; code[used++] = 16;
            code[used++] = 0x86;
            code[used++] = 0x56; // i64.gt_u
            code[used++] = 0x04; code[used++] = 0x40; code[used++] = 0x00; code[used++] = 0x0b;
            return used;
        }

        // The caller has verified that this is a native descriptor and
        // established its non-null base and element bounds.
        internal static int ReadWithinSpan(byte[] code, int used, int view, int index) =>
            LinearMemoryWasm.Load(code, LinearAddress(code, used, view, index), 0x49);

        internal static int ReadLocation(byte[] code, int used, int location)
        {
            used = OpenLocation(code, used, location, I32);
            used = LinearMemoryWasm.Load(code, used, 0x49);
            code[used++] = 0x05;
            used = ManagedLocationWasm.AccessElement(code, used, location, Chars);
            used = Gc(code, used, 0x0d, Chars);
            code[used++] = 0x0b;
            return used;
        }

        internal static int WriteLocation(byte[] code, int used, int location, int value)
        {
            used = OpenLocation(code, used, location, Void);
            used = LinearMemoryWasm.Store(code, Local(code, used, 0x20, value), 0x53);
            code[used++] = 0x05;
            used = ManagedLocationWasm.AccessElement(code, used, location, Chars);
            used = Gc(code, Local(code, used, 0x20, value), 0x0e, Chars);
            code[used++] = 0x0b;
            return used;
        }

        private static int OpenAccess(byte[] code, int used, int view, int index, int type)
        {
            used = SpanWasm.CheckIndex(code, used, view, index);
            return NullOwnerBranch(code, SpanWasm.LoadField(code, used, view, 0), type);
        }

        private static int OpenLocation(byte[] code, int used, int location, int type)
        {
            used = GuardNull(code, used, location);
            used = NullOwnerBranch(code, ManagedLocationWasm.LoadField(code, used, location, 0), type);
            used = ManagedLocationWasm.LoadField(code, used, location, 1);
            code[used++] = 0x45; // i32.eqz
            used = ExceptionTagsWasm.ThrowIf(code, used, ExceptionTagsWasm.NullTag);
            return ManagedLocationWasm.LoadField(code, used, location, 1);
        }

        private static int NullOwnerBranch(byte[] code, int used, int type)
        {
            code[used++] = 0xd1; // ref.is_null
            code[used++] = 0x04; // if
            if (type != Void) return WasmBinary.ValueType(code, used, type);
            code[used++] = 0x40;
            return used;
        }

        private static int NullOwner(byte[] code, int used)
        {
            code[used++] = 0xd0; // ref.null eq
            code[used++] = 0x6d;
            return used;
        }

        internal static int LinearAddress(byte[] code, int used, int view, int index)
        {
            used = SpanWasm.LoadField(code, used, view, 1);
            used = Local(code, used, 0x20, index);
            used = Const(code, used, 1);
            code[used++] = 0x74; // i32.shl
            code[used++] = 0x6a; // i32.add
            return used;
        }
    }
}
