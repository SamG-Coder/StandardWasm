namespace Sample
{
    public static class Kernels
    {
        public static int SumSquares(int n)
        {
            int sum = 0;
            for (int i = 1; i <= n; i++)
                sum += Square(i);
            return sum;
        }

        private static int Square(int x) => x * x;

        public static int Gcd(int a, int b) => b == 0 ? a : Gcd(b, a % b);

        public static int Countdown(int remaining, int total) =>
            remaining <= 0 ? total : Countdown(remaining - 1, total + 1);

        public static int Mix(int value, int shift) =>
            (value << shift) ^ ShiftRight(value, 32 - shift);

        // Keep both shifts observable in the CLR oracle, including when shift is a multiple of 32.
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static int ShiftRight(int value, int shift) => (int)((uint)value >> shift);

        public static bool Between(int value, int lo, int hi) => value >= lo && value <= hi;
    }
}


