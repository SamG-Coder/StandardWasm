using BaseExceptionHandlers;
using BootstrapLibrary;

internal static class BaseExceptionCases
{
    internal static void Add(List<object> cases)
    {
        const int capacity = 8192;
        byte[] image = File.ReadAllBytes(typeof(Handlers).Assembly.Location);
        object encoded = new { bytes = Array.ConvertAll(image, value => (int)value) };
        (int, int)[] division = [(9, 3), (7, 0), (int.MinValue, -1)];
        Check(nameof(Handlers.Ordered), Handlers.Ordered, division);
        Check(nameof(Handlers.ArrayAccess), Handlers.ArrayAccess, [(2, 0), (2, 2), (0, 0), (-1, 0)]);
        Check(nameof(Handlers.Rethrow), Handlers.Rethrow, division);
        (int, int)[] receivers = [(7, 0), (9, 1), (int.MinValue, 2)];
        Check(nameof(Handlers.CatchBindingFailure), Handlers.CatchBindingFailure, receivers);
        Check(nameof(Handlers.CatchArgumentFailure), Handlers.CatchArgumentFailure, receivers);

        void Check(string method, Func<int, int, int> oracle, (int, int)[] inputs)
        {
            int row = typeof(Handlers).GetMethod(method)!.MetadataToken & 0xffffff;
            var output = new byte[capacity];
            int length = CompilerExports.Compile(image, row, output);
            if (length <= 0) throw new InvalidOperationException($"{method} compiler returned {length}");
            string hex = Convert.ToHexString(output.AsSpan(0, length)).ToLowerInvariant();
            foreach (var (a, b) in inputs)
                cases.Add(new
                {
                    module = "BootstrapLibrary", method = "Compile", fixture = method,
                    inputs = new[] { encoded, row },
                    generated = new { capacity, arguments = new[] { a, b }, hex },
                    expected = oracle(a, b)
                });
        }
    }
}
