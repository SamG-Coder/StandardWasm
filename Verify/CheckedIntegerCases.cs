#nullable enable

using System.Globalization;
using BootstrapLibrary;
using CheckedIntegers;

internal static class CheckedIntegerCases
{
    internal static void Add(List<object> cases)
    {
        const int capacity = 8192;
        byte[] image = File.ReadAllBytes(typeof(Arithmetic).Assembly.Location);
        var encoded = new { bytes = Array.ConvertAll(image, value => (int)value) };
        int[] narrow = [int.MinValue, int.MinValue + 1, -46341, -1, 0, 1, 2, 46341, int.MaxValue];
        long[] wide = [long.MinValue, long.MinValue + 1, -3037000500L, -1, 0, 1, 2, 3037000500L, long.MaxValue];
        var pairs32 = (from a in narrow from b in narrow select (a, b)).ToList();
        var pairs64 = (from a in wide from b in wide select (a, b)).ToList();
        pairs32.AddRange([(46340, 46340), (-46340, 46340), (65535, 65535), (65536, 65536)]);
        pairs64.AddRange([(3037000499L, 3037000499L), (-3037000499L, 3037000499L),
            (4294967295L, 4294967295L), (4294967296L, 4294967296L)]);
        var random = new Random(44);
        for (int i = 0; i < 12; i++)
        {
            pairs32.Add((unchecked((int)random.NextInt64(int.MinValue, (long)int.MaxValue + 1)),
                i % 2 == 0 ? random.Next(-3, 4) : unchecked((int)random.NextInt64(int.MinValue, (long)int.MaxValue + 1))));
            pairs64.Add((random.NextInt64(long.MinValue, long.MaxValue),
                i % 2 == 0 ? random.Next(-3, 4) : random.NextInt64(long.MinValue, long.MaxValue)));
        }

        Check32(nameof(Arithmetic.AddI32), Arithmetic.AddI32);
        Check32(nameof(Arithmetic.SubI32), Arithmetic.SubI32);
        Check32(nameof(Arithmetic.MulI32), Arithmetic.MulI32);
        CheckUnsigned32(nameof(Arithmetic.AddU32), Arithmetic.AddU32);
        CheckUnsigned32(nameof(Arithmetic.SubU32), Arithmetic.SubU32);
        CheckUnsigned32(nameof(Arithmetic.MulU32), Arithmetic.MulU32);
        Check64(nameof(Arithmetic.AddI64), Arithmetic.AddI64);
        Check64(nameof(Arithmetic.SubI64), Arithmetic.SubI64);
        Check64(nameof(Arithmetic.MulI64), Arithmetic.MulI64);
        CheckUnsigned64(nameof(Arithmetic.AddU64), Arithmetic.AddU64);
        CheckUnsigned64(nameof(Arithmetic.SubU64), Arithmetic.SubU64);
        CheckUnsigned64(nameof(Arithmetic.MulU64), Arithmetic.MulU64);

        void Check32(string method, Func<int, int, int> operation) =>
            Check(method, pairs32.Select(pair => (new object[] { pair.a, pair.b },
                (Func<object>)(() => operation(pair.a, pair.b)))));

        void CheckUnsigned32(string method, Func<uint, uint, uint> operation) =>
            Check32(method, (a, b) => unchecked((int)operation((uint)a, (uint)b)));

        void Check64(string method, Func<long, long, long> operation) =>
            Check(method, pairs64.Select(pair => (new[] { Encode(pair.a), Encode(pair.b) },
                (Func<object>)(() => Encode(operation(pair.a, pair.b))))));

        void CheckUnsigned64(string method, Func<ulong, ulong, ulong> operation) =>
            Check64(method, (a, b) => unchecked((long)operation((ulong)a, (ulong)b)));

        void Check(string method, IEnumerable<(object[] Arguments, Func<object> Oracle)> fixtures)
        {
            int row = typeof(Arithmetic).GetMethod(method)!.MetadataToken & 0xffffff;
            var output = new byte[capacity];
            int length = CompilerExports.Compile(image, row, output);
            if (length <= 0) throw new InvalidOperationException($"{method} compiler returned {length}");
            string hex = Convert.ToHexString(output.AsSpan(0, length)).ToLowerInvariant();
            foreach (var (arguments, oracle) in fixtures)
            {
                object? expected;
                string? exception = null;
                try { expected = oracle(); }
                catch (OverflowException error)
                {
                    expected = null;
                    exception = error.GetType().FullName;
                }
                cases.Add(new
                {
                    module = "BootstrapLibrary", method = "Compile", fixture = method,
                    inputs = new object[] { encoded, row }, generated = new { capacity, arguments, hex }, expected, exception
                });
            }
        }
    }

    private static object Encode(long value) => new { i64 = value.ToString(CultureInfo.InvariantCulture) };
}
