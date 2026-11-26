#nullable enable

using ArrayBounds;
using BootstrapLibrary;

internal static class ArrayBoundsCases
{
    internal static void Add(List<object> cases)
    {
        const int capacity = 8192;
        byte[] image = File.ReadAllBytes(typeof(Kernels).Assembly.Location);
        object encoded = new { bytes = Array.ConvertAll(image, value => (int)value) };
        (int Length, int Index)[] positions = [(2, 0), (2, 1), (0, 0), (2, -1), (2, 2), (2, int.MaxValue)];
        CheckPairs(nameof(Kernels.Read), positions, Kernels.Read);
        CheckPairs(nameof(Kernels.CatchRead), positions, Kernels.CatchRead);
        CheckPairs(nameof(Kernels.StoreByte), [(0, -1), (0, 256), (-1, 7), (1, 7)], Kernels.StoreByte);
        CheckPairs(nameof(Kernels.Rethrow), [(0, 0), (0, -1), (0, 1), (1, 0), (1, int.MaxValue)], Kernels.Rethrow);
        Check(nameof(Kernels.StoreWide), new[] { -1, 0, 1, int.MinValue }.Select(index =>
            (new object?[] { index }, (Func<int>)(() => Kernels.StoreWide(index)))));
        Check(nameof(Kernels.NullArray), new[] { -1, 0 }.Select(index =>
            (new object?[] { null, index }, (Func<int>)(() => Kernels.NullArray(null!, index)))));

        void CheckPairs(string method, (int A, int B)[] inputs, Func<int, int, int> oracle) =>
            Check(method, inputs.Select(pair => (new object?[] { pair.A, pair.B }, (Func<int>)(() => oracle(pair.A, pair.B)))));

        void Check(string method, IEnumerable<(object?[] Arguments, Func<int> Oracle)> fixtures)
        {
            int row = typeof(Kernels).GetMethod(method)!.MetadataToken & 0xffffff;
            var output = new byte[capacity];
            int length = CompilerExports.Compile(image, row, output);
            if (length <= 0) throw new InvalidOperationException($"{method} compiler returned {length}");
            string hex = Convert.ToHexString(output.AsSpan(0, length)).ToLowerInvariant();
            foreach (var (arguments, oracle) in fixtures)
            {
                int? expected = null;
                string? exception = null, exceptionType = null;
                try { expected = oracle(); }
                catch (IndexOutOfRangeException error) { exception = exceptionType = error.GetType().FullName; }
                catch (NullReferenceException error) { exception = exceptionType = error.GetType().FullName; }
                catch (OverflowException error) { exception = exceptionType = error.GetType().FullName; }
                cases.Add(new
                {
                    module = "BootstrapLibrary", method = "Compile", fixture = method,
                    inputs = new[] { encoded, row }, generated = new { capacity, arguments, hex, exceptionType },
                    expected, exception
                });
            }
        }
    }
}
