#nullable enable

using BootstrapLibrary;
using ReferenceLibrary;

internal static class ReferenceFieldCases
{
    internal static void Add(List<object> cases)
    {
        const int capacity = 16384;
        byte[] image = File.ReadAllBytes(typeof(Links).Assembly.Location);
        var encoded = new { bytes = Array.ConvertAll(image, value => (int)value) };
        var output = new byte[capacity];

        foreach (int value in new[] { int.MinValue, -1, 0, 41, int.MaxValue })
            Check(nameof(Links.Chain), [value], () => Links.Chain(value));
        Check(nameof(Links.Aliases), [1], () => Links.Aliases(true) ? 1 : 0);
        Check(nameof(Links.Aliases), [0], () => Links.Aliases(false) ? 1 : 0);
        Check(nameof(Links.NullRead), [], () => Links.NullRead());

        void Check(string method, int[] arguments, Func<int> oracle)
        {
            int row = typeof(Links).GetMethod(method)!.MetadataToken & 0xffffff;
            int length = CompilerExports.Compile(image, row, output);
            if (length <= 0) throw new InvalidOperationException($"{method} returned {length}");
            string hex = Convert.ToHexString(output.AsSpan(0, length)).ToLowerInvariant();

            int? expected;
            string? exception = null;
            try { expected = oracle(); }
            catch (NullReferenceException error)
            {
                expected = null;
                exception = error.GetType().FullName;
            }

            cases.Add(new
            {
                module = "BootstrapLibrary", method = "Compile", fixture = method,
                inputs = new object[] { encoded, row },
                generated = new { capacity, arguments, hex, exceptionType = exception }, expected, exception
            });
        }
    }
}
