using static PortableCompiler.BranchWasm;
using static PortableCompiler.SignatureTypes;

namespace PortableCompiler
{
    // The pair keeps the target alive and reads/writes its current contents.
    // It names the original location even when copied or passed to a callee.
    internal static class ManagedLocationWasm
    {
        internal const int TypeBytes = 6;

        internal static int WriteType(byte[] output, int cursor)
        {
            output[cursor++] = 0x5f; // struct
            output[cursor++] = 2;
            output[cursor++] = 0x6d; // traced owner, eqref
            output[cursor++] = 0;
            output[cursor++] = 0x7f; // element index, i32
            output[cursor++] = 0;
            return cursor;
        }

        internal static int TakeElement(byte[] code, int used, int array, int index, int result)
        {
            used = GuardNull(code, used, array);
            used = GuardArrayIndex(code, used, array, index, false);
            used = Local(code, used, 0x20, array);
            used = Local(code, used, 0x20, index);
            used = Gc(code, used, 0, ObjectLocation); // struct.new
            return Local(code, used, 0x21, result);
        }

        internal static int TakeCell(byte[] code, int used, int cell, int result, int type)
        {
            used = Local(code, used, 0x20, cell);
            used = Const(code, used, 0);
            used = Gc(code, used, 0, type);
            return Local(code, used, 0x21, result);
        }

        internal static int LoadCell(byte[] code, int used, int cell, int vector, int type)
        {
            used = Local(code, used, 0x20, cell);
            used = Const(code, used, 0);
            used = Gc(code, used, vector == Bytes ? 0x0d : 0x0b, vector);
            return vector == Bytes || vector == Ints || vector == NativeVector ? used : CastCellValue(code, used, type);
        }

        internal static int CastCellValue(byte[] code, int used, int representation)
        {
            if (representation == PeLibrary.ObjectLayouts.ObjectReference) return used;
            code[used++] = 0xfb;
            code[used++] = 0x17; // Restore the typed reference from eqref.
            return WasmBinary.Signed(code, used, WasmBinary.HeapType(representation));
        }

        // Leave [owner, index] for the array instruction. The owner and its
        // bounds were checked when the alias was created. Wasm arrays cannot
        // resize, and only this emitter constructs location values.
        internal static int AccessElement(byte[] code, int used, int location, int array)
        {
            used = GuardNull(code, used, location);
            used = LoadField(code, used, location, 0);
            code[used++] = 0xfb;
            code[used++] = 0x16; // ref.cast, nonnull
            used = WasmBinary.Signed(code, used, WasmBinary.HeapType(array));
            return LoadField(code, used, location, 1);
        }

        internal static int OffsetStaticByte(byte[] code, int used, int location, int offset)
        {
            used = SpanWasm.OpenLocation(code, used, location, StaticByteLocation);
            used = LoadField(code, used, location, 0);
            used = LoadField(code, used, location, 1);
            used = Local(code, used, 0x20, offset);
            code[used++] = 0x6a; // native-width byte offset
            used = Gc(code, used, 0, StaticByteLocation);
            code[used++] = 0x0b;
            return used;
        }

        internal static int OffsetCharacter(byte[] code, int used, int location, int offset)
        {
            used = Local(code, used, 0x20, location);
            code[used++] = 0xd1; // ref.is_null
            code[used++] = 0x04;
            used = WasmBinary.ValueType(code, used, CharacterLocation);
            // Null plus zero stays null. A nonzero result is an ownerless byte address.
            used = Local(code, used, 0x20, offset);
            code[used++] = 0x45; // i32.eqz
            code[used++] = 0x04;
            used = WasmBinary.ValueType(code, used, CharacterLocation);
            code[used++] = 0xd0;
            used = WasmBinary.Signed(code, used, WasmBinary.HeapType(CharacterLocation));
            code[used++] = 0x05;
            code[used++] = 0xd0;
            code[used++] = 0x6d; // ref.null eq
            used = Local(code, used, 0x20, offset);
            used = Gc(code, used, 0, CharacterLocation);
            code[used++] = 0x0b;

            code[used++] = 0x05;
            used = LoadField(code, used, location, 0);
            used = LoadField(code, used, location, 1);
            used = LoadField(code, used, location, 0);
            code[used++] = 0xd1; // An absent owner uses byte units.
            code[used++] = 0x04;
            code[used++] = 0x7f;
            used = Local(code, used, 0x20, offset);
            code[used++] = 0x05;
            used = Local(code, used, 0x20, offset);
            used = Const(code, used, 1);
            code[used++] = 0x75; // i32.shr_s: aligned negative offsets remain negative.
            code[used++] = 0x0b;
            code[used++] = 0x6a;
            used = Gc(code, used, 0, CharacterLocation);
            code[used++] = 0x0b;
            return used;
        }

        internal static int TestCharacterAddress(byte[] code, int used, int location)
        {
            // A character location can also encode a native address as [null, address].
            // The first null test is already on the stack, so never read a null carrier.
            code[used++] = 0x04; code[used++] = 0x7f;
            used = Const(code, used, 1);
            code[used++] = 0x05;
            used = LoadField(code, used, location, 0);
            code[used++] = 0xd1;
            used = LoadField(code, used, location, 1);
            code[used++] = 0x45;
            code[used++] = 0x71;
            code[used++] = 0x0b;
            return used;
        }

        internal static int ReadStaticUInt16(byte[] code, int used, int location)
        {
            // Wasm is little-endian. Read two adjacent bytes at any alignment
            // without making the address a pointer into linear memory.
            used = AccessElement(code, used, location, Bytes);
            used = Gc(code, used, 0x0d, Bytes);
            used = AccessElement(code, used, location, Bytes);
            used = Const(code, used, 1);
            code[used++] = 0x6a;
            used = Gc(code, used, 0x0d, Bytes);
            used = Const(code, used, 8);
            code[used++] = 0x74;
            code[used++] = 0x72;
            return used;
        }

        internal static int LoadField(byte[] code, int used, int location, byte field)
        {
            used = Local(code, used, 0x20, location);
            used = Gc(code, used, 2, ObjectLocation); // struct.get
            code[used++] = field;
            return used;
        }
    }
}
