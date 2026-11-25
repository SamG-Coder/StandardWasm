using LongIntegers;

// CLR oracle for stage 18. A 64-bit input or result is written as {"i64": "<signed decimal>"} because a JSON
// number is exact only to 2^53. ulong values are written as the signed reading of the same 64 bits, which is
// how the Wasm JS API returns an i64 as a BigInt. expected == null means the CLR threw ArithmeticException.
internal static class LongCases
{
    private static object Encode(object value) => value switch
    {
        long v => new Dictionary<string, string> { ["i64"] = v.ToString() },
        ulong v => new Dictionary<string, string> { ["i64"] = unchecked((long)v).ToString() },
        bool v => v ? 1 : 0,
        int v => v,
        _ => throw new ArgumentException($"Unsupported oracle value {value.GetType()}")
    };

    internal static void Add(List<object> cases)
    {
        void Record(string method, object[] inputs, Func<object> run)
        {
            object expected = null;
            string exception = null;
            try { expected = Encode(run()); }
            catch (ArithmeticException e) { exception = e.GetType().FullName; }
            cases.Add(new { module = "LongIntegers", method, inputs = Array.ConvertAll(inputs, Encode), expected, exception });
        }

        long[] boundaries =
        [
            long.MinValue, long.MinValue + 1, -(1L << 32), -(1L << 31) - 1, -3, -2, -1, 0, 1, 2, 3,
            int.MaxValue, 1L << 31, uint.MaxValue, 1L << 32, long.MaxValue - 1, long.MaxValue
        ];
        void Pair(long a, long b)
        {
            ulong ua = unchecked((ulong)a), ub = unchecked((ulong)b);
            Record("Add", [a, b], () => Wide.Add(a, b));
            Record("Multiply", [a, b], () => Wide.Multiply(a, b));
            Record("Divide", [a, b], () => Wide.Divide(a, b));
            Record("Remainder", [a, b], () => Wide.Remainder(a, b));
            Record("DivideUnsigned", [ua, ub], () => Wide.DivideUnsigned(ua, ub));
            Record("RemainderUnsigned", [ua, ub], () => Wide.RemainderUnsigned(ua, ub));
            Record("Compare", [a, b], () => Wide.Compare(a, b));
            Record("CompareUnsigned", [ua, ub], () => Wide.CompareUnsigned(ua, ub));
        }
        void Single(long a)
        {
            ulong ua = unchecked((ulong)a);
            Record("Negate", [a], () => Wide.Negate(a));
            Record("Complement", [a], () => Wide.Complement(a));
            Record("IsZero", [a], () => Wide.IsZero(a));
            Record("Narrow", [a], () => Wide.Narrow(a));
            Record("LowByte", [a], () => Wide.LowByte(a));
            Record("HighHalf", [ua], () => Wide.HighHalf(ua));
            // Counts past 63 and negative counts check that the masking matches C#.
            foreach (int count in new[] { 0, 1, 31, 32, 33, 63, 64, 65, -1 })
            {
                Record("ShiftLeft", [a, count], () => Wide.ShiftLeft(a, count));
                Record("ShiftRight", [a, count], () => Wide.ShiftRight(a, count));
                Record("ShiftRightUnsigned", [ua, count], () => Wide.ShiftRightUnsigned(ua, count));
            }
        }
        foreach (long a in boundaries)
        {
            foreach (long b in boundaries)
                Pair(a, b);
            Single(a);
        }

        var random = new Random(1407);
        long NextLong() => unchecked((long)((ulong)random.NextInt64() << 1 ^ (ulong)random.Next(2)));
        for (int i = 0; i < 200; i++)
        {
            long a = NextLong();
            // Mix full-range divisors with small ones (including zero) so quotients span both extremes.
            long b = i % 2 == 0 ? NextLong() : random.Next(-40, 41);
            Pair(a, b);
            Single(a);
            long m = random.NextInt64(1, 1L << 62);
            Record("MultiplyModulo", [a, b, m], () => Wide.MultiplyModulo(a, b, m));
        }

        foreach (int value in new[] { int.MinValue, -1, 0, 1, int.MaxValue })
        {
            Record("Widen", [value], () => Wide.Widen(value));
            Record("WidenUnsigned", [value], () => Wide.WidenUnsigned(value));
        }
        foreach (var (seed, count) in new[] { (0, 0), (1, 4), (int.MinValue, 3), (-1, 1000), (12345, 257) })
            Record("Fnv1a", [seed, count], () => Wide.Fnv1a(seed, count));
        // Cubes overflow int32 from n = 304 and wrap the unchecked int64 sum past n = 92681.
        foreach (int n in new[] { -1, 0, 1, 303, 304, 1000, 92681, 100000 })
            Record("SumOfCubes", [n], () => Wide.SumOfCubes(n));
        // Keep the CLR recursion shallow. verify.mjs runs the million-deep i64 tail call in each engine.
        Record("Countdown", [300L, 7L], () => Wide.Countdown(300, 7));
    }
}
