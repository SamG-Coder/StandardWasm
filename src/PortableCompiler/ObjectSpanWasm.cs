using static PortableCompiler.BranchWasm;
using static PortableCompiler.SignatureTypes;

namespace PortableCompiler
{
    internal static class ObjectSpanWasm
    {
        internal const int TypeBytes = 8;

        internal static int WriteType(byte[] output, int cursor)
        {
            output[cursor++] = 0x5f; // struct
            output[cursor++] = 3;
            output[cursor++] = 0x6d; // traced owner
            output[cursor++] = 0;
            output[cursor++] = 0x7f; // start index
            output[cursor++] = 0;
            output[cursor++] = 0x7f; // length
            output[cursor++] = 0;
            return cursor;
        }

        internal static int Create(byte[] code, int used, int owner, int length, int target)
        {
            used = Local(code, used, 0x20, owner);
            used = Const(code, used, 0);
            used = Local(code, used, 0x20, length);
            used = Gc(code, used, 0, ObjectSpan); // struct.new
            return Local(code, used, 0x21, target);
        }

        internal static int Length(byte[] code, int used, int view)
        {
            // A default-initialized span is empty even without a descriptor.
            used = Local(code, used, 0x20, view);
            code[used++] = 0xd1; // ref.is_null
            code[used++] = 0x04;
            code[used++] = 0x7f; // if (result i32)
            used = Const(code, used, 0);
            code[used++] = 0x05;
            used = LoadField(code, used, view, 2);
            code[used++] = 0x0b;
            return used;
        }

        internal static int Read(byte[] code, int used, int vector, int view, int index)
        {
            used = Local(code, used, 0x20, index);
            used = Length(code, used, view);
            code[used++] = 0x4f; // i32.ge_u rejects negative indices too
            used = ExceptionTagsWasm.ThrowIf(code, used, ExceptionTagsWasm.IndexTag);
            used = LoadField(code, used, view, 0);
            code[used++] = 0xfb;
            code[used++] = 0x16; // ref.cast nonnull
            used = WasmBinary.Signed(code, used, WasmBinary.HeapType(vector));
            used = LoadField(code, used, view, 1);
            used = Local(code, used, 0x20, index);
            code[used++] = 0x6a; // i32.add
            return Gc(code, used, 0x0b, vector); // array.get
        }

        private static int LoadField(byte[] code, int used, int view, byte field)
        {
            used = Local(code, used, 0x20, view);
            used = Gc(code, used, 2, ObjectSpan); // struct.get
            code[used++] = field;
            return used;
        }
    }
}
