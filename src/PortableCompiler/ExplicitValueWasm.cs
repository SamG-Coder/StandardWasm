using PeLibrary;
using static PeLibrary.ObjectLayouts;
using static PortableCompiler.BranchWasm;
using static PortableCompiler.SignatureTypes;

namespace PortableCompiler
{
    internal static class ExplicitValueWasm
    {
        // The receiver is already materialized, including for default values.
        internal static int Load(byte[] code, int used, int[] objects, int type, int offset, int storage, int scratch)
        {
            used = ReadField(code, used, type, 0, I64);
            used = MoveBits(code, used, offset, 0x88); // i64.shr_u
            if (!IsValueType(objects, storage)) return DecodeScalar(code, used, storage);

            // Use the i64 spill at the replaced evaluation-stack depth. Only
            // the overlay reference was live there on entry to this instruction.
            used = Local(code, used, 0x21, scratch);
            int start = 0;
            for (int i = 0; i < FieldCount(objects, storage); i++)
            {
                int field = FieldTypeAt(objects, storage, i);
                if (field == 0) continue;
                used = Local(code, used, 0x20, scratch);
                used = MoveBits(code, used, start, 0x88);
                used = DecodeScalar(code, used, field);
                start += ExplicitValueLayouts.Width(field);
            }
            return Gc(code, used, 0, storage);
        }

        internal static int Update(byte[] code, int used, int[] objects, int type, int local, int offset, int storage,
            int replacement, byte replacementType)
        {
            int width = ExplicitValueLayouts.ViewWidth(objects, storage);
            if (width != 8)
            {
                used = ValueFieldsWasm.Materialize(code, used, type, local);
                used = ReadField(code, used, type, 0, I64);
                used = KeepOtherBytes(code, used, offset, width);
                code[used++] = 0x83; // i64.and
            }

            if (IsValueType(objects, storage))
                used = Pack(code, used, objects, storage, replacement);
            else
            {
                used = LoadOperand(code, used, replacement, replacementType, storage);
                used = EncodeScalar(code, used, storage);
            }
            used = MoveBits(code, used, offset, 0x86); // i64.shl
            if (width != 8) code[used++] = 0x84; // i64.or

            // Publish a new immutable aggregate so stored copies keep their bytes.
            used = Gc(code, used, 0, type);
            return Local(code, used, 0x21, local);
        }

        private static int Pack(byte[] code, int used, int[] objects, int type, int source)
        {
            used = ValueFieldsWasm.Materialize(code, used, type, source);
            used = Local(code, used, 0x21, source);
            int start = 0, ordinal = 0;
            for (int i = 0; i < FieldCount(objects, type); i++)
            {
                int field = FieldTypeAt(objects, type, i);
                if (field == 0) continue;
                used = Local(code, used, 0x20, source);
                used = ReadField(code, used, type, ordinal++, field);
                used = EncodeScalar(code, used, field);
                used = MoveBits(code, used, start, 0x86);
                if (start != 0) code[used++] = 0x84; // i64.or
                start += ExplicitValueLayouts.Width(field);
            }
            return used;
        }

        private static int DecodeScalar(byte[] code, int used, int storage)
        {
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

        private static int EncodeScalar(byte[] code, int used, int storage)
        {
            if (storage == F64)
                code[used++] = 0xbd; // i64.reinterpret_f64
            else if (storage != I64)
            {
                if (storage == F32) code[used++] = 0xbc; // i32.reinterpret_f32
                code[used++] = 0xad; // i64.extend_i32_u
            }
            if (IsNarrow(storage))
            {
                used = WideConst(code, used, ByteMask(ExplicitValueLayouts.Width(storage)), 0);
                code[used++] = 0x83; // i64.and
            }
            return used;
        }

        private static int KeepOtherBytes(byte[] code, int used, int offset, int width)
        {
            int lowMask = ByteMask(width), highMask = width > 4 ? ByteMask(width - 4) : 0;
            int shift = offset * 8;
            // Split the i64 mask. C# masks shift counts, including a shift by 32.
            int low = shift < 32 ? lowMask << shift : 0;
            int high = shift >= 32 ? lowMask << (shift - 32) :
                shift == 0 ? highMask : (int)((uint)lowMask >> (32 - shift)) | (highMask << shift);
            return WideConst(code, used, ~low, ~high);
        }

        private static int ByteMask(int width) => width >= 4 ? -1 : (1 << (width * 8)) - 1;

        private static int ReadField(byte[] code, int used, int type, int ordinal, int storage)
        {
            used = Gc(code, used, FieldAccess.LoadInstruction(storage), type);
            return WasmBinary.Unsigned(code, used, ordinal);
        }

        private static int MoveBits(byte[] code, int used, int offset, byte instruction)
        {
            if (offset == 0) return used;
            used = WideConst(code, used, offset * 8, 0);
            code[used++] = instruction;
            return used;
        }
    }
}
