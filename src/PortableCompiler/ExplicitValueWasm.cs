using PeLibrary;
using static PortableCompiler.BranchWasm;
using static PortableCompiler.SignatureTypes;

namespace PortableCompiler
{
    internal static class ExplicitValueWasm
    {
        // The receiver on the stack is materialized even for a default value.
        // CLI byte offsets select bits in the little-endian backing cell.
        internal static int Load(byte[] code, int used, int type, int offset, int storage)
        {
            used = ReadCell(code, used, type);
            if (offset != 0)
            {
                used = WideConst(code, used, offset * 8, 0);
                code[used++] = 0x88; // i64.shr_u
            }

            if (storage == F64)
                code[used++] = 0xbf; // f64.reinterpret_i64
            else if (storage != I64)
            {
                code[used++] = 0xa7; // i32.wrap_i64
                if (storage == F32)
                    code[used++] = 0xbe; // f32.reinterpret_i32
                else if (storage == Signed8 || storage == Signed16)
                    code[used++] = storage == Signed8 ? (byte)0xc0 : (byte)0xc1;
                else if (IsNarrow(storage))
                {
                    used = Const(code, used, storage == Unsigned8 ? 0xff : 0xffff);
                    code[used++] = 0x71; // i32.and
                }
            }
            return used;
        }

        internal static int Update(byte[] code, int used, int type, int local, int offset, int storage,
            int replacement, byte replacementType)
        {
            int width = ExplicitValueLayouts.Width(storage);
            int mask = width >= 4 ? -1 : (1 << (width * 8)) - 1;
            int shift = offset * 8;
            if (width != 8)
            {
                used = ValueFieldsWasm.Materialize(code, used, type, local);
                used = ReadCell(code, used, type);
                // Form the 64-bit mask from two words. C# masks shift counts,
                // so handle the zero and cross-word cases explicitly.
                int low = shift < 32 ? mask << shift : 0;
                int high = shift >= 32 ? mask << (shift - 32) :
                    shift == 0 ? 0 : (int)((uint)mask >> (32 - shift));
                used = WideConst(code, used, ~low, ~high);
                code[used++] = 0x83; // i64.and
            }

            used = LoadOperand(code, used, replacement, replacementType, storage);
            if (storage == F64)
                code[used++] = 0xbd; // i64.reinterpret_f64
            else if (storage != I64)
            {
                if (storage == F32) code[used++] = 0xbc; // i32.reinterpret_f32
                code[used++] = 0xad; // i64.extend_i32_u
            }
            if (width < 4)
            {
                used = WideConst(code, used, mask, 0);
                code[used++] = 0x83; // i64.and
            }
            if (offset != 0)
            {
                used = WideConst(code, used, shift, 0);
                code[used++] = 0x86; // i64.shl
            }
            if (width != 8) code[used++] = 0x84; // i64.or

            // Store a new immutable aggregate so value copies retain their bytes.
            used = Gc(code, used, 0, type);
            return Local(code, used, 0x21, local);
        }

        private static int ReadCell(byte[] code, int used, int type)
        {
            used = Gc(code, used, 2, type);
            return WasmBinary.Unsigned(code, used, 0);
        }
    }
}
