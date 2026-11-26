#nullable enable

using System.Globalization;
using System.Numerics;
using System.Reflection;
using BootstrapLibrary;
using CheckedCasts;

internal static class CheckedCastCases
{
    internal static void Add(List<object> cases)
    {
        const int capacity = 8192;
        byte[] image = File.ReadAllBytes(typeof(Conversions).Assembly.Location);
        var encoded = new { bytes = Array.ConvertAll(image, value => (int)value) };
        foreach (var method in typeof(Conversions).GetMethods(BindingFlags.Public | BindingFlags.Static))
        {
            int row = method.MetadataToken & 0xffffff;
            var output = new byte[capacity];
            int length = CompilerExports.Compile(image, row, output);
            if (length <= 0) throw new InvalidOperationException($"{method.Name} compiler returned {length}");
            string hex = Convert.ToHexString(output.AsSpan(0, length)).ToLowerInvariant();
            TypeCode source = Type.GetTypeCode(method.GetParameters()[0].ParameterType);
            var (minimum, maximum) = Bounds(Type.GetTypeCode(method.ReturnType));
            foreach (object input in Inputs(source, minimum, maximum))
            {
                object? expected;
                string? exception = null;
                try { expected = Encode(method.Invoke(null, [input])!); }
                catch (TargetInvocationException error) when (error.InnerException is OverflowException)
                {
                    expected = null;
                    exception = error.InnerException.GetType().FullName;
                }
                cases.Add(new
                {
                    module = "BootstrapLibrary", method = "Compile", fixture = method.Name,
                    inputs = new object[] { encoded, row }, generated = new { capacity, arguments = new[] { Encode(input) }, hex },
                    expected, exception
                });
            }
        }
    }

    private static IEnumerable<object> Inputs(TypeCode source, BigInteger minimum, BigInteger maximum)
    {
        if (source is TypeCode.Single or TypeCode.Double)
        {
            double lower = (double)minimum, upper = (double)(maximum + 1);
            double[] edges = [lower - 1, lower, upper, -1, 0];
            double[] common = [double.NaN, double.NegativeInfinity, double.PositiveInfinity,
                -0.0, 0.0, -1.125, -0.875, 1.125, lower - 0.875, upper - 0.125,
                -double.Epsilon, double.Epsilon, -float.Epsilon, float.Epsilon];
            return source == TypeCode.Single
                ? common.Select(value => (float)value)
                    .Concat(edges.Select(value => (float)value).SelectMany(value => new[] { MathF.BitDecrement(value), value, MathF.BitIncrement(value) }))
                    .DistinctBy(BitConverter.SingleToInt32Bits).Cast<object>()
                : common.Concat(edges.SelectMany(value => new[] { Math.BitDecrement(value), value, Math.BitIncrement(value) }))
                    .DistinctBy(BitConverter.DoubleToInt64Bits).Cast<object>();
        }

        var (start, end) = Bounds(source);
        BigInteger[] values = [start, start + 1, end - 1, end, -1, 0, 1, minimum - 1, minimum, minimum + 1,
            maximum - 1, maximum, maximum + 1];
        return values.Where(value => value >= start && value <= end).Distinct().Select(value => source switch
        {
            TypeCode.Int32 => (object)(int)value,
            TypeCode.UInt32 => (uint)value,
            TypeCode.Int64 => (long)value,
            TypeCode.UInt64 => (ulong)value,
            _ => throw new InvalidOperationException($"Unexpected source type {source}")
        });
    }

    private static (BigInteger Minimum, BigInteger Maximum) Bounds(TypeCode type) => type switch
    {
        TypeCode.SByte => (sbyte.MinValue, sbyte.MaxValue),
        TypeCode.Byte => (byte.MinValue, byte.MaxValue),
        TypeCode.Int16 => (short.MinValue, short.MaxValue),
        TypeCode.UInt16 => (ushort.MinValue, ushort.MaxValue),
        TypeCode.Int32 => (int.MinValue, int.MaxValue),
        TypeCode.UInt32 => (uint.MinValue, uint.MaxValue),
        TypeCode.Int64 => (long.MinValue, long.MaxValue),
        TypeCode.UInt64 => (ulong.MinValue, ulong.MaxValue),
        _ => throw new InvalidOperationException($"Unexpected integer type {type}")
    };

    private static object Encode(object value) => value switch
    {
        uint bits => unchecked((int)bits),
        long bits => new { i64 = bits.ToString(CultureInfo.InvariantCulture) },
        ulong bits => new { i64 = unchecked((long)bits).ToString(CultureInfo.InvariantCulture) },
        float real => new { f64 = ((double)real).ToString("G17", CultureInfo.InvariantCulture) },
        double real => new { f64 = real.ToString("G17", CultureInfo.InvariantCulture) },
        _ => value
    };
}
