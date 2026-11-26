using System;

namespace MathIntrinsics
{
    // Ordinary numeric code that calls System.Math. Each Math call below lowers to a Wasm instruction (decision
    // 0003), so the module still has no imports.
    public static class Measures
    {
        public static double Sqrt(double x) => Math.Sqrt(x);

        public static double Abs(double x) => Math.Abs(x);

        public static float AbsSingle(float x) => Math.Abs(x);

        public static double Floor(double x) => Math.Floor(x);

        public static double Ceiling(double x) => Math.Ceiling(x);

        public static double Truncate(double x) => Math.Truncate(x);

        // Banker's rounding: 0.5 -> 0, 1.5 -> 2, 2.5 -> 2, -0.5 -> -0.
        public static double Round(double x) => Math.Round(x);

        public static double Max(double a, double b) => Math.Max(a, b);

        public static double Min(double a, double b) => Math.Min(a, b);

        public static float MaxSingle(float a, float b) => Math.Max(a, b);

        public static int MaxInt(int a, int b) => Math.Max(a, b);

        public static int MinInt(int a, int b) => Math.Min(a, b);

        public static int MaxUnsigned(int a, int b) => unchecked((int)Math.Max((uint)a, (uint)b));

        public static long MinLong(long a, long b) => Math.Min(a, b);

        public static long MaxUnsignedLong(long a, long b) => unchecked((long)Math.Max((ulong)a, (ulong)b));

        // CLR: OverflowException for int.MinValue. Wasm: trap.
        public static int AbsInt(int x) => Math.Abs(x);

        public static long AbsLong(long x) => Math.Abs(x);

        // Ordinary uses.
        public static double Hypotenuse(double x, double y) => Math.Sqrt(x * x + y * y);

        // Larger real root of a*x^2 + b*x + c, or NaN when there is none.
        public static double LargerRoot(double a, double b, double c)
        {
            double discriminant = b * b - 4 * a * c;
            if (discriminant < 0) return double.NaN;
            return (-b + Math.Sqrt(discriminant)) / (2 * a);
        }

        public static double Clamp(double value, double low, double high) => Math.Min(Math.Max(value, low), high);

        public static int AbsoluteDifference(int a, int b) => Math.Abs(a - b);

        // Round to a number of cents with banker's rounding, as an accounting routine might.
        public static double RoundCents(double amount) => Math.Round(amount * 100) / 100;

        // Integer grid cell for a coordinate, which needs Floor for negative inputs.
        public static int Cell(double coordinate, double size) => (int)Math.Floor(coordinate / size);
    }

    // Negative fixture: an unmapped BCL method must stay an Unlinked call diagnostic.
    public static class UnmappedMath
    {
        public static double Power(double x, double y) => Math.Pow(x, y);
    }
}
