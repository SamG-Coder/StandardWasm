using System.Globalization;
using MathIntrinsics;

// CLR oracle for stage 29. Encodings match FloatCases ({"f64"} as 17 significant digits) and LongCases ({"i64"}).
// expected == null means the CLR threw ArithmeticException (OverflowException from Math.Abs), and Wasm must trap.
internal static class MathCases
{
    private static object Encode(object value) => value switch
    {
        double v => new Dictionary<string, string> { ["f64"] = v.ToString("G17", CultureInfo.InvariantCulture) },
        float v => new Dictionary<string, string> { ["f64"] = ((double)v).ToString("G17", CultureInfo.InvariantCulture) },
        long v => new Dictionary<string, string> { ["i64"] = v.ToString(CultureInfo.InvariantCulture) },
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
            cases.Add(new { module = "MathIntrinsics", method, inputs = Array.ConvertAll(inputs, Encode), expected, exception });
        }

        double[] values =
        [
            0.0, -0.0, 0.5, -0.5, 1.5, -1.5, 2.5, -2.5, 0.49999999999999994, 2.675, 1.0, -1.0, 3.0, 4.0, 1e-310,
            -1e-310, 4503599627370495.5 /* 2^52 - 0.5 */, 4503599627370497.0, 1e300, -1e300, double.MaxValue,
            double.Epsilon, double.PositiveInfinity, double.NegativeInfinity, double.NaN
        ];
        foreach (double a in values)
        {
            Record("Sqrt", [a], () => Measures.Sqrt(a));
            Record("Abs", [a], () => Measures.Abs(a));
            Record("AbsSingle", [(float)a], () => Measures.AbsSingle((float)a));
            Record("Floor", [a], () => Measures.Floor(a));
            Record("Ceiling", [a], () => Measures.Ceiling(a));
            Record("Truncate", [a], () => Measures.Truncate(a));
            Record("Round", [a], () => Measures.Round(a));
            Record("RoundCents", [a], () => Measures.RoundCents(a));
            Record("Cell", [a, 0.75], () => Measures.Cell(a, 0.75));
            foreach (double b in values)
            {
                Record("Max", [a, b], () => Measures.Max(a, b));
                Record("Min", [a, b], () => Measures.Min(a, b));
                Record("MaxSingle", [(float)a, (float)b], () => Measures.MaxSingle((float)a, (float)b));
                Record("Hypotenuse", [a, b], () => Measures.Hypotenuse(a, b));
                Record("Clamp", [a, -1.0, b], () => Measures.Clamp(a, -1.0, b));
            }
        }
        foreach (var (qa, qb, qc) in new[] { (1.0, -3.0, 2.0), (1.0, 2.0, 1.0), (1.0, 0.0, 1.0), (2.0, 4.0, -6.0), (1e-8, 1.0, 1.0), (0.0, 1.0, 1.0) })
            Record("LargerRoot", [qa, qb, qc], () => Measures.LargerRoot(qa, qb, qc));

        int[] ints = [int.MinValue, int.MinValue + 1, -2, -1, 0, 1, 2, int.MaxValue - 1, int.MaxValue];
        foreach (int a in ints)
        {
            Record("AbsInt", [a], () => Measures.AbsInt(a));
            Record("AbsLong", [(long)a], () => Measures.AbsLong(a));
            foreach (int b in ints)
            {
                Record("MaxInt", [a, b], () => Measures.MaxInt(a, b));
                Record("MinInt", [a, b], () => Measures.MinInt(a, b));
                Record("MaxUnsigned", [a, b], () => Measures.MaxUnsigned(a, b));
                Record("AbsoluteDifference", [a, b], () => Measures.AbsoluteDifference(a, b));
            }
        }
        long[] longs = [long.MinValue, long.MinValue + 1, -(1L << 32), -1, 0, 1, 1L << 32, long.MaxValue];
        foreach (long a in longs)
        {
            Record("AbsLong", [a], () => Measures.AbsLong(a));
            foreach (long b in longs)
            {
                Record("MinLong", [a, b], () => Measures.MinLong(a, b));
                Record("MaxUnsignedLong", [a, b], () => Measures.MaxUnsignedLong(a, b));
            }
        }

        var random = new Random(2929);
        for (int i = 0; i < 300; i++)
        {
            double a = BitConverter.Int64BitsToDouble(random.NextInt64(long.MinValue, long.MaxValue));
            double b = random.NextDouble() * 2000 - 1000;
            Record("Sqrt", [a], () => Measures.Sqrt(a));
            Record("Round", [b], () => Measures.Round(b));
            Record("Floor", [a], () => Measures.Floor(a));
            Record("Max", [a, b], () => Measures.Max(a, b));
            Record("Hypotenuse", [b, a], () => Measures.Hypotenuse(b, a));
        }
    }
}
