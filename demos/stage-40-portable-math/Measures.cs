using System;

namespace PortableMath
{
    // Wasm-hosted compiler entry points, as in stage 30.
    public static class MathDemo
    {
        public static byte[] CreateBytes(int length) => new byte[length];
        public static void SetByte(byte[] image, int index, int value) => image[index] = unchecked((byte)value);
        public static int ReadByte(byte[] image, int index) => image[index];
        public static int FindRow(byte[] image, byte[] name) => PeLibrary.MethodNames.FindRow(image, name);
        public static int Compile(byte[] image, int row, byte[] output) =>
            PortableCompiler.MethodCompiler.Compile(image, row, output);
    }

    // Ordinary numeric code over every System.Math overload in decision 0003. The self-hosted compiler lowers each
    // call to Wasm, including the managed overflow path for integer Abs.
    public static class Measures
    {
        public static double Hypotenuse(double x, double y) => Math.Sqrt(x * x + y * y);
        public static double Abs(double x) => Math.Abs(x);
        public static float AbsSingle(float x) => Math.Abs(x);
        public static double Floor(double x) => Math.Floor(x);
        public static double Ceiling(double x) => Math.Ceiling(x);
        public static double Truncate(double x) => Math.Truncate(x);
        public static double RoundCents(double amount) => Math.Round(amount * 100) / 100;
        public static double Round(double x) => Math.Round(x);
        public static double Clamp(double value, double low, double high) => Math.Min(Math.Max(value, low), high);
        public static float ClampSingle(float value, float low, float high) => Math.Min(Math.Max(value, low), high);
        public static int MaxInt(int a, int b) => Math.Max(a, b);
        public static int MinInt(int a, int b) => Math.Min(a, b);
        public static int MaxUnsigned(int a, int b) => unchecked((int)Math.Max((uint)a, (uint)b));
        public static int MinUnsigned(int a, int b) => unchecked((int)Math.Min((uint)a, (uint)b));
        public static long MaxLong(long a, long b) => Math.Max(a, b);
        public static long MinLong(long a, long b) => Math.Min(a, b);
        public static long MaxUnsignedLong(long a, long b) => unchecked((long)Math.Max((ulong)a, (ulong)b));
        public static long MinUnsignedLong(long a, long b) => unchecked((long)Math.Min((ulong)a, (ulong)b));
        public static int AbsInt(int x) => Math.Abs(x);
        public static long AbsLong(long x) => Math.Abs(x);
        public static int CatchAbsInt(int x)
        {
            try { return Math.Abs(x); }
            catch (OverflowException) { return 123; }
        }

        public static long CatchAbsLong(long x)
        {
            try { return AbsLong(x); }
            catch (OverflowException) { return 456; }
        }

        public static int AbsoluteDifference(int a, int b) => Math.Abs(a - b);
        public static int Cell(double coordinate, double size) => (int)Math.Floor(coordinate / size);

        // Outside the exact table: must stay a diagnostic at the call.
        public static int Sign(int value) => Math.Sign(value);
    }
}
