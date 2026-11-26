namespace EmitterLibrary
{
    public static class Kernels
    {
        public static int Combine(int a, int b) => (a * b + 12345678) ^ -193;
        public static int Mix(int a, int b) => ~((a ^ -b) >> 5);
        public static int Constant() => int.MinValue;
        public static int Quotient(int dividend, int divisor) => dividend / divisor;
        public static short UnsupportedSignature(int value) => unchecked((short)value);
        public static int UnsupportedCall(int value) => System.Math.Abs(value);
    }
}
