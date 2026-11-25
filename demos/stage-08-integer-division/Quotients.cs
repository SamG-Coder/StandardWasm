namespace IntegerDivision
{
    // Integer division boundary fixture. Every method compiles to a single IL div, div.un, rem or rem.un
    // (or a short loop of them), so each CLR result or exception can be compared with the Wasm module.
    //
    // CLR semantics exercised here (ECMA-335 III.3.31, III.3.55):
    //   - Signed quotients truncate toward zero, and the remainder takes the sign of the dividend.
    //   - A zero divisor throws DivideByZeroException for all four instructions.
    //   - int.MinValue / -1 and int.MinValue % -1 throw OverflowException (ArithmeticException).
    // Wasm i32.div_s traps on both failures. i32.rem_s returns 0 for MinValue % -1 instead of
    // trapping, so the compiler guards that pair explicitly.
    public static class Quotients
    {
        public static int Divide(int dividend, int divisor) => dividend / divisor;

        public static int Remainder(int dividend, int divisor) => dividend % divisor;

        // Reinterpreting the bits as uint makes high-bit inputs large positive values (div.un / rem.un).
        public static int DivideUnsigned(int dividend, int divisor) => (int)((uint)dividend / (uint)divisor);

        public static int RemainderUnsigned(int dividend, int divisor) => (int)((uint)dividend % (uint)divisor);

        // Constant divisors keep the IL instruction (Roslyn does not fold or strength-reduce them).
        // -1 reaches the signed overflow case with a literal operand.
        public static int DivideByNegativeOne(int value) => value / -1;

        public static int RemainderByNegativeOne(int value) => value % -1;

        // A power-of-two divisor must still truncate toward zero. An arithmetic shift would round down.
        public static int DivideByEight(int value) => value / 8;

        public static int RemainderBySeven(int value) => value % 7;

        public static int DivideUnsignedByTen(int value) => (int)((uint)value / 10u);

        // The CLR guarantees (a / b) * b + a % b == a whenever both operations succeed.
        public static bool QuotientIdentityHolds(int dividend, int divisor) =>
            unchecked(Divide(dividend, divisor) * divisor + Remainder(dividend, divisor)) == dividend;

        // Ordinary use: sum of the decimal digits of the unsigned bit pattern.
        public static int UnsignedDigitSum(int value)
        {
            uint remaining = (uint)value;
            int sum = 0;
            while (remaining != 0)
            {
                sum += (int)(remaining % 10u);
                remaining /= 10u;
            }
            return sum;
        }
    }
}
