#nullable enable

using System.Reflection.Emit;
using BootstrapLibrary;
using InstanceNull;

internal static class InstanceNullCases
{
    internal static void Add(List<object> cases)
    {
        const int capacity = 16384;
        byte[] image = File.ReadAllBytes(typeof(Kernels).Assembly.Location);
        object encoded = new { bytes = Array.ConvertAll(image, value => (int)value) };
        var body = typeof(Kernels).GetMethod(nameof(Kernels.Invoke))!.GetMethodBody()!;
        if (IlOffsets.Find(body.GetILAsByteArray()!, OpCodes.Callvirt) < 0)
            throw new InvalidOperationException("Instance fixture must use callvirt");

        Check(nameof(Kernels.Invoke), [
            (new object?[] { null, 2 }, () => Kernels.Invoke(null!, 2)),
            (new object?[] { null, int.MaxValue }, () => Kernels.Invoke(null!, int.MaxValue))
        ]);
        (int Present, int Argument)[] calls = [(0, 2), (1, 2), (0, -1), (1, -1), (0, int.MaxValue), (1, int.MaxValue)];
        Check(nameof(Kernels.CallCleanup), calls.Select(pair =>
            (new object?[] { pair.Present, pair.Argument }, (Func<int>)(() => Kernels.CallCleanup(pair.Present, pair.Argument)))));
        Check(nameof(Kernels.ExchangeCleanup), new[] { 0, 1 }.Select(present =>
            (new object?[] { present }, (Func<int>)(() => Kernels.ExchangeCleanup(present)))));

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
                catch (NullReferenceException error) { exception = error.GetType().FullName; }
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
