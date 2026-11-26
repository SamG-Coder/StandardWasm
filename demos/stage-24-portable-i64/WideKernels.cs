namespace WideLibrary
{
    public static class WideDemo
    {
        public static byte[] CreateBytes(int length) => new byte[length];
        public static void SetByte(byte[] image, int index, int value) => image[index] = unchecked((byte)value);
        public static int ReadByte(byte[] image, int index) => image[index];
        public static int FindRow(byte[] image, byte[] name) => PeLibrary.MethodNames.FindRow(image, name);
        public static int CompileBundle(byte[] bundle, byte[] entries, byte[] output) =>
            PortableCompiler.MethodCompiler.CompileBundle(bundle, entries, output);
    }

    public static class WideKernels
    {
        public static long Calculate(long value, int scale) => unchecked(
            WideMath.Arithmetic.Scale(value, scale) ^ (long)WideMath.Arithmetic.Scale((ulong)value, scale));

        public static long SumOfSquares(int count)
        {
            long sum = 0;
            for (int i = 1; i <= count; i++)
                sum += Square(i);
            return sum;
        }

        private static long Square(long value) => unchecked(value * value);

        public static long Literal(int selector)
        {
            if (selector < 0) return long.MinValue;
            if (selector == 0) return 0x00000000ffffffff;
            if (selector == 1) return -0x0000000100000001;
            return long.MaxValue;
        }

        public static long SignedDivision(long left, long right) => left / right;
        public static long SignedRemainder(long left, long right) => left % right;
        public static ulong UnsignedDivision(ulong left, ulong right) => left / right;
        public static ulong UnsignedRemainder(ulong left, ulong right) => left % right;

        public static long ShiftAndNegate(long value, int count) => unchecked((value << count) - (value >> count) + ~(-value));
        public static ulong UnsignedShift(ulong value, int count) => value >> count;

        public static long Select(long left, long right, int delta) =>
            unchecked((left < right ? left : right) + delta);

        public static bool UnsignedLess(ulong left, ulong right) => left < right;
        public static bool IsZero(long value) => value == 0;
        public static int Truth(long value) => value != 0 ? 7 : 13;

        public static long Widen(int signed, uint unsigned) => signed + (long)unsigned;
        public static int Narrow(long value) => unchecked((int)value);
        public static int NarrowByte(long value) => unchecked((byte)value);

        public static long CheckedAdd(long left, long right) => checked(left + right);
        public static int CheckedNarrow(long value) => checked((int)value);
    }
}
