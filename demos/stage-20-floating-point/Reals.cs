namespace FloatingPoint
{
    // Floating-point fixture. double maps to Wasm f64 and float to f32. Results are compared bit-exactly with the
    // CLR (Object.is), so NaN, -0.0, infinities, subnormals and float rounding all count.
    public static class Reals
    {
        public static double Add(double a, double b) => a + b;

        public static double Subtract(double a, double b) => a - b;

        public static double Multiply(double a, double b) => a * b;

        public static double Divide(double a, double b) => a / b;

        public static double Negate(double a) => -a;

        // float arithmetic stays single precision. This is not double arithmetic rounded at the end.
        public static float AddSingle(float a, float b) => a + b;

        public static float MultiplySingle(float a, float b) => a * b;

        public static float DivideSingle(float a, float b) => a / b;

        // Ordered and unordered comparisons. NaN compares false with every ordered operator.
        // Returns -1, 0 or 1 for ordered operands and 2 when either operand is NaN.
        public static int Compare(double a, double b) => a < b ? -1 : a > b ? 1 : a == b ? 0 : 2;

        // Roslyn emits clt.un and cgt.un for these negated forms.
        public static bool LessOrUnordered(double a, double b) => !(a >= b);

        public static bool GreaterOrUnordered(double a, double b) => !(a <= b);

        public static bool NotEqual(float a, float b) => a != b;

        // Saturating conversions (.NET 9+): NaN becomes 0, out-of-range values clamp.
        public static int ToInt32(double value) => (int)value;

        public static int ToUInt32(double value) => unchecked((int)(uint)value);

        public static long ToInt64(double value) => (long)value;

        public static long ToUInt64(double value) => unchecked((long)(ulong)value);

        public static int ToByte(double value) => (byte)value;

        public static int SingleToInt32(float value) => (int)value;

        public static double FromInt32(int value) => value;

        public static double FromUInt32(int value) => unchecked((uint)value);

        public static double FromInt64(long value) => value;

        public static double FromUInt64(long value) => unchecked((ulong)value);

        public static float SingleFromInt64(long value) => value;

        // C# emits conv.r.un then conv.r4. RyuJIT rounds that adjacent pair once, and the compiler matches it.
        public static float SingleFromUInt64(long value) => unchecked((ulong)value);

        // An explicit (double) step keeps both roundings on the CLR too.
        public static float SingleThroughDouble(long value) => (float)(double)unchecked((ulong)value);

        public static float Demote(double value) => (float)value;

        public static double Promote(float value) => value;

        // Ordinary numeric code: naive and compensated summation of count copies of term.
        public static double NaiveSum(double term, int count)
        {
            double sum = 0;
            for (int i = 0; i < count; i++)
                sum += term;
            return sum;
        }

        public static double KahanSum(double term, int count)
        {
            double sum = 0, compensation = 0;
            for (int i = 0; i < count; i++)
            {
                double y = term - compensation;
                double t = sum + y;
                compensation = (t - sum) - y;
                sum = t;
            }
            return sum;
        }

        // Horner evaluation of 1 + x/2 - x^2/8 + x^3/16 (a sqrt(1 + x) series).
        public static double Polynomial(double x) => 1.0 + x * (0.5 + x * (-0.125 + x * 0.0625));

        // Newton's method for sqrt, bounded so NaN and infinity inputs terminate.
        public static double NewtonSqrt(double value, int iterations)
        {
            if (value < 0) return double.NaN;
            if (value == 0) return value;
            double x = value > 1 ? value : 1;
            for (int i = 0; i < iterations; i++)
            {
                double next = 0.5 * (x + value / x);
                if (next == x) break;
                x = next;
            }
            return x;
        }

        public static float Lerp(float a, float b, float t) => a + (b - a) * t;

        // Clamp written with branches. A NaN value falls through both tests and returns NaN.
        public static double Clamp(double value, double low, double high)
        {
            if (value < low) return low;
            if (value > high) return high;
            return value;
        }

        // Literal constants, including signed zero, infinities and NaN, must survive compilation unchanged.
        public static double Constant(int which)
        {
            // An if chain rather than a C# switch statement, because the IL switch instruction is not lowered yet.
            if (which == 0) return -0.0;
            if (which == 1) return double.PositiveInfinity;
            if (which == 2) return double.NegativeInfinity;
            if (which == 3) return double.NaN;
            if (which == 4) return double.Epsilon;
            if (which == 5) return 0.1;
            if (which == 6) return double.MaxValue;
            // 2^-958: its .NET 10 "R" string does not round-trip, so this checks the compiler's literal formatting.
            if (which == 7) return 4.1045368012983762E-289;
            return float.Epsilon;
        }
    }
}
