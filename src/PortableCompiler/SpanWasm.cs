using static PortableCompiler.BranchWasm;
using static PortableCompiler.SignatureTypes;

namespace PortableCompiler
{
    internal static class SpanWasm
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

        internal static int CreateSingleObject(byte[] code, int used, int owner, int target)
        {
            used = Local(code, used, 0x20, owner);
            used = Const(code, used, 0);
            used = Const(code, used, 1);
            used = Gc(code, used, 0, ObjectSpan);
            return Local(code, used, 0x21, target);
        }

        // Both views retain their original packed UTF-16 owner. A string
        // contributes its backing array. A null source produces an empty view.
        internal static int FromOwner(byte[] code, int used, int source, bool wrapped)
        {
            used = Local(code, used, 0x20, source);
            code[used++] = 0xd1; // ref.is_null
            code[used++] = 0x04;
            used = WasmBinary.ValueType(code, used, CharacterSpan);
            code[used++] = 0xd0; // ref.null
            used = WasmBinary.Signed(code, used, WasmBinary.HeapType(CharacterSpan));
            code[used++] = 0x05;
            used = Local(code, used, 0x20, source);
            if (wrapped) used = StringWasm.Unwrap(code, used);
            used = Const(code, used, 0);
            used = Local(code, used, 0x20, source);
            if (wrapped) used = StringWasm.Unwrap(code, used);
            code[used++] = 0xfb;
            code[used++] = 0x0f; // array.len
            used = Gc(code, used, 0, CharacterSpan);
            code[used++] = 0x0b;
            return used;
        }

        internal static int FromStaticBytes(byte[] code, int used, int offset, int length, int backing)
        {
            used = Const(code, used, offset);
            used = Const(code, used, backing);
            used = Gc(code, used, 9, Bytes); // fresh packed owner from the passive data segment
            code[used++] = 0;
            used = Const(code, used, 0);
            used = Const(code, used, length);
            return Gc(code, used, 0, ReadOnlyByteSpan);
        }

        internal static int TakeStaticByteLocation(byte[] code, int used, int view)
        {
            used = OpenByteLocation(code, used, view);
            used = LoadField(code, used, view, 0);
            used = LoadField(code, used, view, 1);
            used = Gc(code, used, 0, StaticByteLocation);
            code[used++] = 0x0b;
            return used;
        }

        // GetReference(default) yields a null location. Taking or advancing
        // the address doesn't dereference its owner.
        internal static int OpenByteLocation(byte[] code, int used, int local)
        {
            used = Local(code, used, 0x20, local);
            code[used++] = 0xd1; // ref.is_null
            code[used++] = 0x04;
            used = WasmBinary.ValueType(code, used, StaticByteLocation);
            code[used++] = 0xd0; // ref.null
            used = WasmBinary.Signed(code, used, WasmBinary.HeapType(StaticByteLocation));
            code[used++] = 0x05;
            return used;
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
            used = Address(code, used, vector, view, index);
            return Gc(code, used, vector == Chars || vector == Bytes ? 0x0d : 0x0b, vector);
        }

        internal static int CheckIndex(byte[] code, int used, int view, int index)
        {
            used = Local(code, used, 0x20, index);
            used = Length(code, used, view);
            code[used++] = 0x4f; // i32.ge_u rejects negative indices too
            return ExceptionTagsWasm.ThrowIf(code, used, ExceptionTagsWasm.IndexTag);
        }

        internal static int Address(byte[] code, int used, int vector, int view, int index)
        {
            used = CheckIndex(code, used, view, index);
            return CheckedArrayAddress(code, used, vector, view, index);
        }

        internal static int CheckedArrayAddress(byte[] code, int used, int vector, int view, int index)
        {
            used = LoadField(code, used, view, 0);
            code[used++] = 0xfb;
            code[used++] = 0x16; // ref.cast nonnull
            used = WasmBinary.Signed(code, used, WasmBinary.HeapType(vector));
            used = LoadField(code, used, view, 1);
            used = Local(code, used, 0x20, index);
            code[used++] = 0x6a; // i32.add
            return used;
        }

        internal static int LoadField(byte[] code, int used, int view, byte field)
        {
            used = Local(code, used, 0x20, view);
            used = Gc(code, used, 2, ObjectSpan); // struct.get
            code[used++] = field;
            return used;
        }
    }
}
