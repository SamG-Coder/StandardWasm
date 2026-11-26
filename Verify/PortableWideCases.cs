#nullable enable

using System.Buffers.Binary;
using System.Globalization;
using BootstrapLibrary;
using WideLibrary;

internal static class PortableWideCases
{
    internal static void Add(List<object> cases)
    {
        const int capacity = 32768;
        byte[] image = File.ReadAllBytes(typeof(WideKernels).Assembly.Location);
        byte[] dependency = File.ReadAllBytes(typeof(WideMath.Arithmetic).Assembly.Location);
        byte[] bundle = BundleCases.Pack(image, dependency);
        object encodedImage = EncodeBytes(image);
        object encodedBundle = EncodeBytes(bundle);
        var outputs = new Dictionary<string, string>();
        var output = new byte[capacity];

        long[] boundaries = [long.MinValue, long.MinValue + 1, -9007199254740993L, -1L, 0L, 1L, 9007199254740993L, long.MaxValue];
        foreach (long value in boundaries)
        {
            Case(nameof(WideKernels.Select), [value, 9007199254740993L, -1], () => WideKernels.Select(value, 9007199254740993L, -1));
            Case(nameof(WideKernels.UnsignedLess), [value, 1L], () => WideKernels.UnsignedLess((ulong)value, 1));
            Case(nameof(WideKernels.Truth), [value], () => WideKernels.Truth(value));
            Case(nameof(WideKernels.IsZero), [value], () => WideKernels.IsZero(value));
            Case(nameof(WideKernels.Narrow), [value], () => WideKernels.Narrow(value));
            Case(nameof(WideKernels.NarrowByte), [value], () => WideKernels.NarrowByte(value));

            int count = value < 0 ? -65 : 64;
            Case(nameof(WideKernels.ShiftAndNegate), [value, count], () => WideKernels.ShiftAndNegate(value, count));
            Case(nameof(WideKernels.UnsignedShift), [value, count], () => WideKernels.UnsignedShift((ulong)value, count));
            Case(nameof(WideKernels.Calculate), [value, count], () => WideKernels.Calculate(value, count), linked: true);

            foreach (long divisor in new[] { -1L, 0L, 3L })
            {
                Case(nameof(WideKernels.SignedDivision), [value, divisor], () => WideKernels.SignedDivision(value, divisor));
                Case(nameof(WideKernels.SignedRemainder), [value, divisor], () => WideKernels.SignedRemainder(value, divisor));
                Case(nameof(WideKernels.UnsignedDivision), [value, divisor], () => WideKernels.UnsignedDivision((ulong)value, (ulong)divisor));
                Case(nameof(WideKernels.UnsignedRemainder), [value, divisor], () => WideKernels.UnsignedRemainder((ulong)value, (ulong)divisor));
            }
        }
        foreach (int n in new[] { -1, 0, 1, 2, 300001 })
        {
            Case(nameof(WideKernels.Literal), [n], () => WideKernels.Literal(n));
            Case(nameof(WideKernels.SumOfSquares), [n], () => WideKernels.SumOfSquares(n));
        }
        Case(nameof(WideKernels.Widen), [int.MinValue, -1], () => WideKernels.Widen(int.MinValue, uint.MaxValue));
        Case(nameof(WideKernels.Widen), [-1, int.MinValue], () => WideKernels.Widen(-1, 0x80000000));
        Case(nameof(WideKernels.CheckedAdd), [long.MaxValue, 1L], () => WideKernels.CheckedAdd(long.MaxValue, 1));
        Case(nameof(WideKernels.CheckedAdd), [long.MinValue, 1L], () => WideKernels.CheckedAdd(long.MinValue, 1));
        Case(nameof(WideKernels.CheckedNarrow), [(long)int.MinValue], () => WideKernels.CheckedNarrow(int.MinValue));
        Case(nameof(WideKernels.CheckedNarrow), [1L + int.MaxValue], () => WideKernels.CheckedNarrow(1L + int.MaxValue));

        void Case(string method, object[] args, Func<object> evaluate, bool linked = false)
        {
            int row = Row(method);
            byte[] entries = new byte[11];
            BinaryPrimitives.WriteInt32LittleEndian(entries, row);
            entries[4] = 3;
            "run"u8.CopyTo(entries.AsSpan(8));

            if (!outputs.TryGetValue(method, out string? hex))
            {
                int length = linked
                    ? CompilerExports.CompileBundle(bundle, entries, output)
                    : CompilerExports.Compile(image, row, output);
                if (length <= 0) throw new InvalidOperationException($"{method}: compiler returned {length}");
                hex = Convert.ToHexString(output.AsSpan(0, length)).ToLowerInvariant();
                outputs.Add(method, hex);
            }

            object? expected;
            string? exception = null;
            try { expected = Encode(evaluate()); }
            catch (ArithmeticException error)
            {
                expected = null;
                exception = error.GetType().FullName;
            }
            cases.Add(new
            {
                module = "BootstrapLibrary", method = linked ? "CompileBundle" : "Compile", fixture = method,
                inputs = linked ? new[] { encodedBundle, EncodeBytes(entries) } : new[] { encodedImage, (object)row },
                generated = new { capacity, arguments = Array.ConvertAll(args, Encode), hex, bundle = linked,
                    exceptionType = exception == typeof(OverflowException).FullName ? exception : null },
                expected, exception
            });
        }

    }

    private static int Row(string method) => typeof(WideKernels).GetMethod(method)!.MetadataToken & 0xffffff;

    private static object EncodeBytes(byte[] bytes) => new { bytes = Array.ConvertAll(bytes, value => (int)value) };

    private static object Encode(object value) => value switch
    {
        long bits => new { i64 = bits.ToString(CultureInfo.InvariantCulture) },
        ulong bits => new { i64 = unchecked((long)bits).ToString(CultureInfo.InvariantCulture) },
        bool condition => condition ? 1 : 0,
        _ => value
    };
}
