using System.Globalization;
using FloatingPoint;

// CLR oracle for stage 20. Floating-point inputs and results are written as {"f64": "<17 significant digits>"},
// which also covers NaN, the infinities and -0 (JSON cannot carry them). A float is written through its exact
// double value, so the JS Number converts back to the same binary32 on entry. Results are compared with
// Object.is, which makes the comparison bit-exact except for NaN payloads.
internal static class FloatCases
{
    private static object Encode(object value) => value switch
    {
        // G17 always round-trips. "R" does not on .NET 10 for some values, e.g. 2^-958 (see FloatOps.Literal).
        double v => new Dictionary<string, string> { ["f64"] = v.ToString("G17", CultureInfo.InvariantCulture) },
        float v => new Dictionary<string, string> { ["f64"] = ((double)v).ToString("G17", CultureInfo.InvariantCulture) },
        long v => new Dictionary<string, string> { ["i64"] = v.ToString(CultureInfo.InvariantCulture) },
        bool v => v ? 1 : 0,
        int v => v,
        _ => throw new ArgumentException($"Unsupported oracle value {value.GetType()}")
    };

    internal static void Add(List<object> cases)
    {
        void Record(string method, object[] inputs, Func<object> run) =>
            cases.Add(new { module = "FloatingPoint", method, inputs = Array.ConvertAll(inputs, Encode), expected = Encode(run()) });

        double[] specials =
        [
            0.0, -0.0, double.Epsilon, -double.Epsilon, 2.2250738585072014E-308 /* min normal */,
            1.0, -1.0, 0.1, 0.2, 0.3, 0.5, 1.5, 2.5, -2.5, 1e-300, 1e300,
            9007199254740991.0 /* 2^53 - 1 */, 9007199254740993.0 /* 2^53 + 1, rounds to even */,
            2147483647.0, 2147483648.0, -2147483648.0, -2147483649.0, 4294967295.0, 4294967296.0,
            9.2233720368547758E18, 1.8446744073709552E19, 255.5, 300.7, -1.0000001,
            double.MaxValue, double.MinValue, double.PositiveInfinity, double.NegativeInfinity, double.NaN
        ];
        float[] singles =
        [
            0f, -0f, float.Epsilon, 1.17549435E-38f /* min normal */, 0.1f, 0.2f, 1f / 3f, 16777217f, 3e38f,
            float.MaxValue, -float.MaxValue, float.PositiveInfinity, float.NegativeInfinity, float.NaN, 2147483520f, 2.1474836E9f
        ];

        foreach (double a in specials)
        {
            foreach (double b in specials)
            {
                Record("Add", [a, b], () => Reals.Add(a, b));
                Record("Subtract", [a, b], () => Reals.Subtract(a, b));
                Record("Multiply", [a, b], () => Reals.Multiply(a, b));
                Record("Divide", [a, b], () => Reals.Divide(a, b));
                Record("Compare", [a, b], () => Reals.Compare(a, b));
                Record("LessOrUnordered", [a, b], () => Reals.LessOrUnordered(a, b));
                Record("GreaterOrUnordered", [a, b], () => Reals.GreaterOrUnordered(a, b));
            }
            Record("Negate", [a], () => Reals.Negate(a));
            Record("ToInt32", [a], () => Reals.ToInt32(a));
            Record("ToUInt32", [a], () => Reals.ToUInt32(a));
            Record("ToInt64", [a], () => Reals.ToInt64(a));
            Record("ToUInt64", [a], () => Reals.ToUInt64(a));
            Record("ToByte", [a], () => Reals.ToByte(a));
            Record("Demote", [a], () => Reals.Demote(a));
            Record("Polynomial", [a], () => Reals.Polynomial(a));
            Record("NewtonSqrt", [a, 60], () => Reals.NewtonSqrt(a, 60));
            Record("Clamp", [a, -1.0, 1.0], () => Reals.Clamp(a, -1.0, 1.0));
        }
        foreach (float a in singles)
        {
            foreach (float b in singles)
            {
                Record("AddSingle", [a, b], () => Reals.AddSingle(a, b));
                Record("MultiplySingle", [a, b], () => Reals.MultiplySingle(a, b));
                Record("DivideSingle", [a, b], () => Reals.DivideSingle(a, b));
                Record("NotEqual", [a, b], () => Reals.NotEqual(a, b));
            }
            Record("SingleToInt32", [a], () => Reals.SingleToInt32(a));
            Record("Promote", [a], () => Reals.Promote(a));
            Record("Lerp", [a, 10f, 0.25f], () => Reals.Lerp(a, 10f, 0.25f));
        }
        foreach (int value in new[] { int.MinValue, -1, 0, 1, 16777217, int.MaxValue })
        {
            Record("FromInt32", [value], () => Reals.FromInt32(value));
            Record("FromUInt32", [value], () => Reals.FromUInt32(value));
        }
        // Values around 2^24 and 2^53 round differently in float and double. For 0x8000008000000001 a single
        // rounding (CLR (float)ulong) and a double rounding (through an explicit double) give different floats.
        foreach (long value in new[] { long.MinValue, -1L, 0L, 1L, (1L << 53) + 1, (1L << 24) + 1, long.MaxValue,
                                       unchecked((long)0x8000008000000001UL), unchecked((long)0xFFFFFFFFFFFFF801UL) })
        {
            Record("FromInt64", [value], () => Reals.FromInt64(value));
            Record("FromUInt64", [value], () => Reals.FromUInt64(value));
            Record("SingleFromInt64", [value], () => Reals.SingleFromInt64(value));
            Record("SingleFromUInt64", [value], () => Reals.SingleFromUInt64(value));
            Record("SingleThroughDouble", [value], () => Reals.SingleThroughDouble(value));
        }
        for (int which = 0; which <= 9; which++)
            Record("Constant", [which], () => Reals.Constant(which));
        foreach (var (term, count) in new[] { (0.1, 10), (0.1, 1000), (1e-16, 100000), (1.0, 0), (double.NaN, 3) })
        {
            Record("NaiveSum", [term, count], () => Reals.NaiveSum(term, count));
            Record("KahanSum", [term, count], () => Reals.KahanSum(term, count));
        }

        var random = new Random(2040);
        for (int i = 0; i < 300; i++)
        {
            // Random bit patterns reach subnormals, huge exponents and NaNs that decimal sampling would miss.
            double a = BitConverter.Int64BitsToDouble(random.NextInt64(long.MinValue, long.MaxValue));
            double b = i % 3 == 0 ? random.NextDouble() * 1000 - 500 : BitConverter.Int64BitsToDouble(random.NextInt64(long.MinValue, long.MaxValue));
            Record("Add", [a, b], () => Reals.Add(a, b));
            Record("Multiply", [a, b], () => Reals.Multiply(a, b));
            Record("Divide", [a, b], () => Reals.Divide(a, b));
            Record("ToInt64", [b], () => Reals.ToInt64(b));
            float fa = BitConverter.Int32BitsToSingle(random.Next(int.MinValue, int.MaxValue));
            float fb = (float)(random.NextDouble() * 200 - 100);
            Record("AddSingle", [fa, fb], () => Reals.AddSingle(fa, fb));
            Record("DivideSingle", [fb, fa], () => Reals.DivideSingle(fb, fa));
        }
    }
}
