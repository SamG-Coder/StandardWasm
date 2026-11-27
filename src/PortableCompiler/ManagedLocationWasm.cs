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
            return type == String ? CastString(code, used) : used;
        }

        internal static int CastString(byte[] code, int used)
        {
            code[used++] = 0xfb;
            code[used++] = 0x17; // ref.cast null string heap
            return WasmBinary.Signed(code, used, WasmBinary.HeapType(String));
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

        private static int LoadField(byte[] code, int used, int location, byte field)
        {
            used = Local(code, used, 0x20, location);
            used = Gc(code, used, 2, ObjectLocation); // struct.get
            code[used++] = field;
            return used;
        }
    }
}
