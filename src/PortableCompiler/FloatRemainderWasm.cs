using static PortableCompiler.BranchWasm;
using static PortableCompiler.SignatureTypes;

namespace PortableCompiler
{
    internal static class FloatRemainderWasm
    {
        internal const int BodyCapacity = 192, TypeSize = 6;
        private const int Dividend = 0, Divisor = 1, Remainder = 2, Magnitude = 3, Step = 4;

        internal static int Call(byte[] code, int used, int bodyStart, int[] head)
        {
            code[used++] = 0x10;
            return WasmIndexFixups.Write(code, used, bodyStart, head);
        }

        internal static int WriteType(byte[] output, int cursor)
        {
            output[cursor++] = 0x60;
            output[cursor++] = 2;
            output[cursor++] = 0x7c;
            output[cursor++] = 0x7c;
            output[cursor++] = 1;
            output[cursor++] = 0x7c;
            return cursor;
        }

        internal static int WriteBody(byte[] code)
        {
            // f64 fmod using the same exact power-of-two scaling as the host
            // emitter. When t <= r < 2t, subtracting t is exact (Sterbenz).
            int used = 0;
            code[used++] = 1;
            code[used++] = 3;
            code[used++] = 0x7c;

            // NaN, infinite dividend and zero divisor all produce NaN.
            used = Pair(code, used, Dividend, Dividend, 0x62);
            used = Pair(code, used, Divisor, Divisor, 0x62);
            code[used++] = 0x72;
            used = Local(code, used, 0x20, Dividend);
            code[used++] = 0x99;
            used = FloatWasm.Literal(code, used, F64, 0, 0x7ff00000);
            code[used++] = 0x61;
            code[used++] = 0x72;
            used = Local(code, used, 0x20, Divisor);
            used = FloatWasm.Literal(code, used, F64, 0, 0);
            code[used++] = 0x61;
            code[used++] = 0x72;
            used = Block(code, used, 0x04);
            used = FloatWasm.Literal(code, used, F64, 0, 0x7ff80000);
            code[used++] = 0x0f;
            code[used++] = 0x0b;

            used = Local(code, used, 0x20, Dividend);
            code[used++] = 0x99;
            used = Local(code, used, 0x21, Remainder);
            used = Local(code, used, 0x20, Divisor);
            code[used++] = 0x99;
            used = Local(code, used, 0x21, Magnitude);
            // The dividend is unchanged for a larger divisor, including infinity.
            used = Pair(code, used, Remainder, Magnitude, 0x63);
            used = Block(code, used, 0x04);
            used = Local(code, used, 0x20, Dividend);
            code[used++] = 0x0f;
            code[used++] = 0x0b;

            used = Local(code, used, 0x20, Magnitude);
            used = Local(code, used, 0x21, Step);
            used = Block(code, used, 0x02);
            used = Block(code, used, 0x03);
            used = Scale(code, used, 0x40000000); // twice the current step
            used = Local(code, used, 0x20, Remainder);
            code[used++] = 0x64;
            code[used++] = 0x0d;
            code[used++] = 1;
            used = Scale(code, used, 0x40000000);
            used = Local(code, used, 0x21, Step);
            used = Repeat(code, used);

            used = Block(code, used, 0x02);
            used = Block(code, used, 0x03);
            used = Pair(code, used, Remainder, Step, 0x66);
            used = Block(code, used, 0x04);
            used = Pair(code, used, Remainder, Step, 0xa1);
            used = Local(code, used, 0x21, Remainder);
            code[used++] = 0x0b;
            used = Pair(code, used, Step, Magnitude, 0x61);
            code[used++] = 0x0d;
            code[used++] = 1;
            used = Scale(code, used, 0x3fe00000); // undo the last doubling
            used = Local(code, used, 0x21, Step);
            used = Repeat(code, used);
            used = Pair(code, used, Remainder, Dividend, 0xa6); // copysign, including negative zero
            code[used++] = 0x0b;
            return used;
        }

        private static int Pair(byte[] code, int used, int left, int right, byte opcode)
        {
            used = Local(code, used, 0x20, left);
            used = Local(code, used, 0x20, right);
            code[used++] = opcode;
            return used;
        }

        private static int Scale(byte[] code, int used, int high)
        {
            used = Local(code, used, 0x20, Step);
            used = FloatWasm.Literal(code, used, F64, 0, high);
            code[used++] = 0xa2;
            return used;
        }

        private static int Block(byte[] code, int used, byte opcode)
        {
            code[used++] = opcode;
            code[used++] = 0x40;
            return used;
        }

        private static int Repeat(byte[] code, int used)
        {
            code[used++] = 0x0c;
            code[used++] = 0;
            code[used++] = 0x0b;
            code[used++] = 0x0b;
            return used;
        }
    }
}
