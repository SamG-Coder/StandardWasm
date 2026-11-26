namespace PortableReals
{
    public static class RealDemo
    {
        public static byte[] CreateBytes(int length) => new byte[length];
        public static void SetByte(byte[] image, int index, int value) => image[index] = unchecked((byte)value);
        public static int ReadByte(byte[] image, int index) => image[index];
        public static int FindRow(byte[] image, byte[] name) => PeLibrary.MethodNames.FindRow(image, name);
        public static int Compile(byte[] image, int row, byte[] output) =>
            PortableCompiler.MethodCompiler.Compile(image, row, output);
    }

    public static class Signals
    {
        public static double Filter(double sample, int steps)
        {
            double value = 1.0;
            for (int i = 0; i < steps; i++)
                value = (value + sample) * 0.5;
            return value;
        }

        public static double Blend(float sample, double delta) => (sample + delta) / 3.0 - sample;
        public static float AddSingle(float left, float right) => left + right;
        public static double Divide(double left, double right) => left / right;
        public static double Negate(double value) => -value;
        public static float NegateSingle(float value) => -value;
        public static double Choose(double left, double right) => left >= right ? left : right;
        public static bool UnorderedOrLess(double left, double right) => !(left >= right);
        public static int Truncate(double value) => unchecked((int)value);
        public static uint TruncateUnsigned(float value) => unchecked((uint)value);
        public static long TruncateWide(double value) => unchecked((long)value);
        public static ulong TruncateUnsignedWide(double value) => unchecked((ulong)value);
        public static float FloatFromUnsigned(ulong value) => value;
        public static float FloatThroughDouble(ulong value) => (float)(double)value;
        public static double DoubleFromSigned(long value) => value;
        public static float Demote(double value) => (float)value;
        public static double TinyConstant() => 4.1045368012983762E-289;
        public static float NegativeZero() => -0.0f;

        public static double Linked(float sample, double delta) =>
            FloatDependency.Scale.Adjust(sample, delta) + FloatDependency.Scale.Adjust(sample, (float)delta);

        public static double Remainder(double left, double right) => left % right;
        public static int CheckedTruncate(double value) => checked((int)value);
    }
}
