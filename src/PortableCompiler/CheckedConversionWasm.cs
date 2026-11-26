using static PortableCompiler.BranchWasm;
using static PortableCompiler.SignatureTypes;

namespace PortableCompiler
{
    internal static class CheckedConversionWasm
    {
        internal static byte Result(int opcode) => TargetWidth(opcode) == 64 ? I64 : I32;

        internal static int Emit(byte[] code, int used, int opcode, byte source, int input, int output)
        {
            bool unsignedSource = opcode <= 0x89;
            bool unsignedTarget = unsignedSource ? opcode >= 0x86 : (opcode & 1) == 0;
            int width = TargetWidth(opcode);
            byte target = Result(opcode);
            if (IsFloat(source))
            {
                used = GuardFloat(code, used, input, source, width, unsignedTarget);
                used = Local(code, used, 0x20, input);
                int operation = target == I64 ? 0xae : 0xa8;
                if (source == F64) operation += 2;
                if (unsignedTarget) operation++;
                code[used++] = unchecked((byte)operation);
                return Local(code, used, 0x21, output);
            }

            used = Guard(code, used, input, source, unsignedSource, width, unsignedTarget);
            used = Local(code, used, 0x20, input);
            if (source == I64 && target == I32) code[used++] = 0xa7; // i32.wrap_i64
            else if (source == I32 && target == I64)
                code[used++] = unsignedSource ? (byte)0xad : (byte)0xac; // i64.extend_i32_u/s
            return Local(code, used, 0x21, output);
        }

        private static int Guard(byte[] code, int used, int input, byte source, bool unsignedSource, int width, bool unsignedTarget)
        {
            int sourceWidth = source == I64 ? 64 : 32;
            if (!unsignedSource && (unsignedTarget || width < sourceWidth))
            {
                int minimum = unsignedTarget ? 0 : unchecked(-(1 << (width - 1)));
                used = Bound(code, used, input, source, minimum, unsignedTarget ? 0 : -1, 0x48); // lt_s
            }

            if (width < sourceWidth || (width == sourceWidth && unsignedSource && !unsignedTarget))
            {
                int valueBits = unsignedTarget ? width : width - 1;
                // Keep the emitter itself within the bootstrap's i32 subset.
                int low = valueBits < 32 ? unchecked((1 << valueBits) - 1) : -1;
                int high = valueBits == 63 ? int.MaxValue : 0;
                used = Bound(code, used, input, source, low, high, unsignedSource ? 0x4b : 0x4a); // gt_u/s
            }
            return used;
        }

        private static int Bound(byte[] code, int used, int input, byte source, int low, int high, int comparison)
        {
            used = Local(code, used, 0x20, input);
            used = source == I64
                ? WideConst(code, used, low, high)
                : Const(code, used, low);
            code[used++] = NumericOpcode(comparison, source);
            return ArithmeticExceptionWasm.ThrowIf(code, used);
        }

        private static int GuardFloat(byte[] code, int used, int input, byte source, int width, bool unsigned)
        {
            // Test the truncated value in its original float width. Powers of
            // two are exact bounds even at i64/u64 limits. NaN fails both tests.
            used = Local(code, used, 0x20, input);
            code[used++] = source == F32 ? (byte)0x8f : (byte)0x9d; // trunc
            used = Local(code, used, 0x22, input);
            used = unsigned ? FloatWasm.Literal(code, used, source, 0, 0)
                : PowerOfTwo(code, used, source, width - 1, int.MinValue);
            code[used++] = source == F32 ? (byte)0x60 : (byte)0x66; // ge
            used = Local(code, used, 0x20, input);
            used = PowerOfTwo(code, used, source, unsigned ? width : width - 1, 0);
            code[used++] = source == F32 ? (byte)0x5d : (byte)0x63; // lt
            code[used++] = 0x71; // and
            code[used++] = 0x45; // eqz
            return ArithmeticExceptionWasm.ThrowIf(code, used);
        }

        private static int PowerOfTwo(byte[] code, int used, byte source, int exponent, int sign) =>
            source == F32
                ? FloatWasm.Literal(code, used, source, sign | ((127 + exponent) << 23), 0)
                : FloatWasm.Literal(code, used, source, 0, sign | ((1023 + exponent) << 20));

        private static int TargetWidth(int opcode) =>
            8 << (opcode <= 0x89 ? (opcode - 0x82) & 3 : (opcode - 0xb3) >> 1);
    }
}
