using System.Globalization;
using FloatRemainder;

// CLR oracle for stage 33. Values use the {"f64": "<17 digits>"} encoding and Object.is comparison.
internal static class RemainderCases
{
    private static object Encode(object value) => value switch
    {
        double v => new Dictionary<string, string> { ["f64"] = v.ToString("G17", CultureInfo.InvariantCulture) },
        float v => new Dictionary<string, string> { ["f64"] = ((double)v).ToString("G17", CultureInfo.InvariantCulture) },
        int v => v,
        _ => throw new ArgumentException($"Unsupported oracle value {value.GetType()}")
    };

    internal static void Add(List<object> cases)
    {
        void Record(string method, object[] inputs, Func<object> run) =>
            cases.Add(new { module = "FloatRemainder", method, inputs = Array.ConvertAll(inputs, Encode), expected = Encode(run()) });

        // The pairs include huge/tiny exponent ratios (the helper's longest loops), subnormal divisors, equal
        // magnitudes, exact multiples (signed zero results) and every special value.
        double[] values =
        [
            0.0, -0.0, 1.0, -1.0, 3.0, -3.0, 7.0, -7.0, 0.1, 0.3, 2.5, 360.0, -30.0, 1e-300, 1e300,
            double.Epsilon, -double.Epsilon, 2.2250738585072014E-308, 2.2250738585072009E-308 /* largest subnormal */,
            double.MaxValue, -double.MaxValue, 9007199254740993.0, 4503599627370496.5,
            double.PositiveInfinity, double.NegativeInfinity, double.NaN
        ];
        foreach (double x in values)
        {
            foreach (double y in values)
            {
                Record("Remainder", [x, y], () => Remainders.Remainder(x, y));
                Record("RemainderSingle", [(float)x, (float)y], () => Remainders.RemainderSingle((float)x, (float)y));
                Record("Phase", [x, y], () => Remainders.Phase(x, y));
            }
            Record("WrapDegrees", [x], () => Remainders.WrapDegrees(x));
            Record("Fraction", [x], () => Remainders.Fraction(x));
        }
        foreach (var (step, count) in new[] { (0.1f, 25), (0.3f, 1000), (1e-7f, 100), (0.75f, 3), (float.NaN, 2) })
            Record("Oscillate", [step, count], () => Remainders.Oscillate(step, count));

        var random = new Random(3333);
        for (int i = 0; i < 400; i++)
        {
            // Random bit patterns reach every exponent pairing, including subnormal and NaN operands.
            double x = BitConverter.Int64BitsToDouble(random.NextInt64(long.MinValue, long.MaxValue));
            double y = i % 4 == 0 ? random.NextDouble() * 10 - 5 : BitConverter.Int64BitsToDouble(random.NextInt64(long.MinValue, long.MaxValue));
            Record("Remainder", [x, y], () => Remainders.Remainder(x, y));
            float fx = BitConverter.Int32BitsToSingle(random.Next(int.MinValue, int.MaxValue));
            float fy = BitConverter.Int32BitsToSingle(random.Next(int.MinValue, int.MaxValue));
            Record("RemainderSingle", [fx, fy], () => Remainders.RemainderSingle(fx, fy));
            Record("WrapDegrees", [x], () => Remainders.WrapDegrees(x));
        }
    }
}
