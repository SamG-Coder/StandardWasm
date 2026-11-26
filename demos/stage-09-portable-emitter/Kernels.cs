namespace EmitterLibrary
{
    public static class Kernels
    {
        public static int Combine(int a, int b) => (a * b + 12345678) ^ -193;
        public static int Mix(int a, int b) => ~((a ^ -b) >> 5);
        public static int Constant() => int.MinValue;
        public static int Quotient(int dividend, int divisor) => dividend / divisor;
        public static short UnsupportedSignature(int value) => unchecked((short)value);
        // Math.Sign is outside the exact intrinsic table (decision 0003), so this call stays unsupported.
        public static int UnsupportedCall(int value) => System.Math.Sign(value);
    }
}
