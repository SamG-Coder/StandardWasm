#nullable enable

using AllocationDelegates;
using BootstrapLibrary;

internal static class AllocationDelegateCases
{
    internal static void Add(List<object> cases)
    {
        const int capacity = 16384;
        byte[] image = File.ReadAllBytes(typeof(Kernels).Assembly.Location);
        object encoded = new { bytes = Array.ConvertAll(image, value => (int)value) };
        CheckScalar(nameof(Kernels.AllocateBytes), [int.MinValue, -1, 0, 1, 257], Kernels.AllocateBytes);
        CheckScalar(nameof(Kernels.AllocateWide), [int.MinValue, -1, 0, 3], Kernels.AllocateWide);
        CheckScalar(nameof(Kernels.AllocationCleanup), [int.MinValue, -1, 0, 1, 17], Kernels.AllocationCleanup);
        CheckScalar(nameof(Kernels.BoundScalar), [int.MinValue, 0, 19], Kernels.BoundScalar);
        CheckScalar(nameof(Kernels.NullSafeEvent), [0, 1], Kernels.NullSafeEvent);
        Check(nameof(Kernels.InvokeScalar), [(new object?[] { null }, () => Kernels.InvokeScalar(null!))]);
        (int Subscribed, int Argument)[] events = [(0, 2), (1, 2), (0, int.MaxValue), (1, int.MaxValue)];
        Check(nameof(Kernels.EventCleanup), events.Select(pair =>
            (new object?[] { pair.Subscribed, pair.Argument },
                (Func<int>)(() => Kernels.EventCleanup(pair.Subscribed, pair.Argument)))));

        void CheckScalar(string method, int[] values, Func<int, int> oracle) =>
            Check(method, values.Select(value => (new object?[] { value }, (Func<int>)(() => oracle(value)))));

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
                string? exception = null;
                try { expected = oracle(); }
                catch (Exception error) when (error is NullReferenceException or OverflowException)
                {
                    exception = error.GetType().FullName;
                }
                cases.Add(new
                {
                    module = "BootstrapLibrary", method = "Compile", fixture = method,
                    inputs = new[] { encoded, row }, generated = new { capacity, arguments, hex, exceptionType = exception },
                    expected, exception
                });
            }
        }
    }
}
