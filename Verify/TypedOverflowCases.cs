#nullable enable

using System.Globalization;
using BootstrapLibrary;
using TypedOverflow;

internal static class TypedOverflowCases
{
    internal static void Add(List<object> cases)
    {
        const int capacity = 8192;
        byte[] image = File.ReadAllBytes(typeof(Faults).Assembly.Location);
        var encoded = new { bytes = Array.ConvertAll(image, value => (int)value) };
        (int Operation, int A, int B)[] branchInputs =
            [(0, 10, 7), (0, int.MaxValue, 1), (0, int.MinValue, -1), (1, 10, 2), (1, 1, 0), (1, int.MinValue, -1)];
        Check(nameof(Faults.AddOrDivide), branchInputs.Select(value =>
            (new object[] { value.Operation, value.A, value.B }, value.Operation == 0,
                (Func<object>)(() => Faults.AddOrDivide(value.Operation, value.A, value.B)))));

        (long A, long B)[] products = [(long.MinValue, -1), (-1, long.MinValue), (long.MinValue, 0), (long.MaxValue, 2), (3, -7)];
        Check(nameof(Faults.Product), products.Select(value =>
            (new[] { Wide(value.A), Wide(value.B) }, true, (Func<object>)(() => Wide(Faults.Product(value.A, value.B))))));

        Check(nameof(Faults.Carry), new[] { 0u, 13u, uint.MaxValue }.Select(value =>
            (new object[] { unchecked((int)value), 1 }, true, (Func<object>)(() => unchecked((int)Faults.Carry(value, 1))))));

        Check(nameof(Faults.ToByte), new[] { -1.0, -0.875, 255.875, 256.0, double.NaN, double.PositiveInfinity }.Select(value =>
            (new[] { Real(value) }, true, (Func<object>)(() => Faults.ToByte(value)))));

        Check(nameof(Faults.RemainderWithGlobal), new[] { 0.0, 2.0 }.Select(divisor =>
            (new[] { Real(17.25), Real(divisor) }, true, (Func<object>)(() => Faults.RemainderWithGlobal(17.25, divisor)))));

        void Check(string method, IEnumerable<(object[] Arguments, bool Typed, Func<object> Oracle)> fixtures)
        {
            int row = typeof(Faults).GetMethod(method)!.MetadataToken & 0xffffff;
            var output = new byte[capacity];
            int length = CompilerExports.Compile(image, row, output);
            if (length <= 0) throw new InvalidOperationException($"{method} compiler returned {length}");
            string hex = Convert.ToHexString(output.AsSpan(0, length)).ToLowerInvariant();
            foreach (var (arguments, typed, oracle) in fixtures)
            {
                object? expected;
                string? exception = null;
                try { expected = oracle(); }
                catch (ArithmeticException error)
                {
                    expected = null;
                    exception = error.GetType().FullName;
                }
                cases.Add(new
                {
                    module = "BootstrapLibrary", method = "Compile", fixture = method,
                    inputs = new object[] { encoded, row }, generated = new { capacity, arguments, hex,
                        exceptionType = typed ? exception : null }, expected, exception
                });
            }
        }
    }

    private static object Wide(long value) => new { i64 = value.ToString(CultureInfo.InvariantCulture) };
    private static object Real(double value) => new { f64 = value.ToString("G17", CultureInfo.InvariantCulture) };
}
