namespace LongIntegers
{
    // 64-bit integer fixture. long and ulong both map to Wasm i64. Each export crosses the JS boundary as a
    // BigInt, which the engine wraps to 64 bits on entry (ToBigInt64). Signedness comes from the IL
    // instruction, so ulong results come back as the signed reading of the same bits.
    public static class Wide
    {
        public static long Add(long a, long b) => unchecked(a + b);

        public static long Multiply(long a, long b) => unchecked(a * b);

        // CLR: DivideByZeroException for b == 0, OverflowException for long.MinValue / -1 and % -1.
        public static long Divide(long a, long b) => a / b;

        public static long Remainder(long a, long b) => a % b;

        public static ulong DivideUnsigned(ulong a, ulong b) => a / b;

        public static ulong RemainderUnsigned(ulong a, ulong b) => a % b;

        // C# masks a long shift count with 63 (Roslyn emits the mask). The IL shift count is int32.
        public static long ShiftLeft(long a, int count) => a << count;

        public static long ShiftRight(long a, int count) => a >> count;

        public static ulong ShiftRightUnsigned(ulong a, int count) => a >> count;

        public static long Negate(long a) => unchecked(-a);

        public static long Complement(long a) => ~a;

        // Signed and unsigned three-way comparison through i64 branches and ceq/clt/cgt.
        public static int Compare(long a, long b) => a < b ? -1 : a > b ? 1 : 0;

        public static int CompareUnsigned(ulong a, ulong b) => a < b ? -1 : a > b ? 1 : 0;

        public static bool IsZero(long a) => a == 0;

        // conv.i8 sign-extends and conv.u8 zero-extends an int32.
        public static long Widen(int value) => value;

        public static long WidenUnsigned(int value) => (uint)value;

        // conv.i4 and conv.u1 keep the low bits of an int64.
        public static int Narrow(long value) => unchecked((int)value);

        public static int LowByte(long value) => unchecked((byte)value);

        public static int HighHalf(ulong value) => unchecked((int)(value >> 32));

        // Ordinary use: 64-bit FNV-1a over the bytes of count consecutive int32 values starting at seed.
        public static ulong Fnv1a(int seed, int count)
        {
            ulong hash = 14695981039346656037UL;
            for (int i = 0; i < count; i++)
            {
                uint word = unchecked((uint)(seed + i));
                for (int shift = 0; shift < 32; shift += 8)
                {
                    hash ^= (word >> shift) & 0xFF;
                    hash = unchecked(hash * 1099511628211UL);
                }
            }
            return hash;
        }

        // Ordinary use: sum of cubes 1..n, which exceeds int32 for n >= 304.
        public static long SumOfCubes(int n)
        {
            long sum = 0;
            for (int i = 1; i <= n; i++)
                sum += (long)i * i * i;
            return sum;
        }

        // (a * b) mod m without 128-bit intermediates, by binary doubling. Requires 0 < m < 2^62.
        public static long MultiplyModulo(long a, long b, long m)
        {
            long result = 0;
            a %= m;
            if (a < 0) a += m;
            b %= m;
            if (b < 0) b += m;
            while (b != 0)
            {
                if ((b & 1) != 0)
                    result = (result + a) % m;
                a = (a << 1) % m;
                b >>= 1;
            }
            return result;
        }

        // Direct tail recursion with i64 parameters lowers to return_call.
        public static long Countdown(long remaining, long total) =>
            remaining <= 0 ? total : Countdown(remaining - 1, total + 3);
    }
}
