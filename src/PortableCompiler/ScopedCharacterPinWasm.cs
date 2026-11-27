using static PortableCompiler.BranchWasm;
using static PortableCompiler.SignatureTypes;

namespace PortableCompiler
{
    internal static class ScopedCharacterPinWasm
    {
        internal const int Capacity = StackAllocationWasm.Capacity + 480;

        internal static int Call(byte[] code, int used, int location, int token, int input, int result, int import,
            int chars, int start, int count, int address, int index, int size, int saved, int scratch,
            int sp, int stackBase, int limit)
        {
            // A native-backed location already names linear memory. When the
            // location has a traced owner, lend the callback a copy of the
            // suffix and publish its writes before any managed IL resumes.
            used = RefNullChars(code, used);
            used = Local(code, used, 0x21, chars);
            used = Const(code, used, 0);
            used = Local(code, used, 0x21, address);
            used = Const(code, used, -1); // Native-backed or null: no traced extent.
            used = Local(code, used, 0x21, count);
            used = Local(code, used, 0x20, location);
            code[used++] = 0xd1; // ref.is_null
            code[used++] = 0x45; // non-null
            code[used++] = 0x04; code[used++] = 0x40;
            used = ManagedLocationWasm.LoadField(code, used, location, 0);
            code[used++] = 0xfb; code[used++] = 0x17;
            used = WasmBinary.Signed(code, used, WasmBinary.HeapType(Chars));
            used = Local(code, used, 0x21, chars);
            used = ManagedLocationWasm.LoadField(code, used, location, 1);
            used = Local(code, used, 0x21, address);
            code[used++] = 0x0b;

            used = IfHasChars(code, used, chars);
            used = Local(code, used, 0x20, address);
            used = Local(code, used, 0x21, start);
            used = Local(code, used, 0x20, chars);
            code[used++] = 0xfb; code[used++] = 0x0f; // array.len
            used = Local(code, used, 0x20, start);
            code[used++] = 0x6b; // length - index, negative suffix fails the unsigned check
            used = Local(code, used, 0x20, start);
            used = Const(code, used, 0);
            code[used++] = 0x48; // i32.lt_s
            code[used++] = 0x04; code[used++] = 0x40; code[used++] = 0x00; code[used++] = 0x0b;
            used = Local(code, used, 0x22, count);
            used = Const(code, used, StackAllocationWasm.Pages << 15);
            code[used++] = 0x4b; // i32.gt_u
            code[used++] = 0x04; code[used++] = 0x40; code[used++] = 0x00; code[used++] = 0x0b;
            used = StackAllocationWasm.Begin(code, used, sp, saved);
            // An empty but non-null location still has a nonzero address.
            used = Local(code, used, 0x20, count);
            used = Const(code, used, 1);
            used = Local(code, used, 0x20, count);
            code[used++] = 0x1b; // select
            used = Const(code, used, 1);
            code[used++] = 0x74; // byte size
            used = Local(code, used, 0x21, size);
            used = StackAllocationWasm.Allocate(code, used, sp, stackBase, limit, size, address, scratch);
            used = Copy(code, used, chars, start, count, address, index, writeBack: false);
            code[used++] = 0x0b;

            used = Local(code, used, 0x20, token);
            used = Local(code, used, 0x20, input);
            used = Local(code, used, 0x20, address);
            used = Local(code, used, 0x20, count);
            code[used++] = 0x10;
            used = WasmBinary.Unsigned(code, used, import);
            used = Local(code, used, 0x21, result);

            used = IfHasChars(code, used, chars);
            used = Copy(code, used, chars, start, count, address, index, writeBack: true);
            used = StackAllocationWasm.Release(code, used, sp, limit, saved);
            code[used++] = 0x0b;
            return used;
        }

        private static int RefNullChars(byte[] code, int used)
        {
            code[used++] = 0xd0;
            return WasmBinary.Signed(code, used, WasmBinary.HeapType(Chars));
        }

        private static int IfHasChars(byte[] code, int used, int chars)
        {
            used = Local(code, used, 0x20, chars);
            code[used++] = 0xd1; code[used++] = 0x45;
            code[used++] = 0x04; code[used++] = 0x40;
            return used;
        }

        private static int Copy(byte[] code, int used, int chars, int start, int count, int address, int index, bool writeBack)
        {
            used = Const(code, used, 0);
            used = Local(code, used, 0x21, index);
            code[used++] = 0x02; code[used++] = 0x40;
            code[used++] = 0x03; code[used++] = 0x40;
            used = Local(code, used, 0x20, index);
            used = Local(code, used, 0x20, count);
            code[used++] = 0x46;
            code[used++] = 0x0d; code[used++] = 1;
            if (writeBack)
            {
                used = ArrayIndex(code, used, chars, start, index);
                used = ByteAddress(code, used, address, index);
                code[used++] = 0x2f; code[used++] = 1; code[used++] = 0; // i32.load16_u
                used = Gc(code, used, 0x0e, Chars); // array.set
            }
            else
            {
                used = ByteAddress(code, used, address, index);
                used = ArrayIndex(code, used, chars, start, index);
                used = Gc(code, used, 0x0d, Chars); // array.get_u
                code[used++] = 0x3b; code[used++] = 1; code[used++] = 0; // i32.store16
            }
            used = Local(code, used, 0x20, index);
            used = Const(code, used, 1);
            code[used++] = 0x6a;
            used = Local(code, used, 0x21, index);
            code[used++] = 0x0c; code[used++] = 0;
            code[used++] = 0x0b; code[used++] = 0x0b;
            return used;
        }

        private static int ArrayIndex(byte[] code, int used, int chars, int start, int index)
        {
            used = Local(code, used, 0x20, chars);
            used = Local(code, used, 0x20, start);
            used = Local(code, used, 0x20, index);
            code[used++] = 0x6a;
            return used;
        }

        private static int ByteAddress(byte[] code, int used, int address, int index)
        {
            used = Local(code, used, 0x20, address);
            used = Local(code, used, 0x20, index);
            used = Const(code, used, 1);
            code[used++] = 0x74; code[used++] = 0x6a;
            return used;
        }
    }
}
