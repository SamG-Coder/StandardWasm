#nullable enable

using BootstrapLibrary;
using NullThrow;

internal static class NullThrowCases
{
    internal static void Add(List<object> cases)
    {
        const int capacity = 8192;
        byte[] image = File.ReadAllBytes(typeof(Cases).Assembly.Location);
        object encoded = new { bytes = Array.ConvertAll(image, value => (int)value) };
        Check(nameof(Cases.Escape), Cases.Escape);
        Check(nameof(Cases.Cleanup), Cases.Cleanup);
        Check(nameof(Cases.FromCall), Cases.FromCall);

        void Check(string method, Func<int, int> oracle)
        {
            int row = typeof(Cases).GetMethod(method)!.MetadataToken & 0xffffff;
            var output = new byte[capacity];
            int length = CompilerExports.Compile(image, row, output);
            if (length <= 0) throw new InvalidOperationException($"{method} compiler returned {length}");
            string hex = Convert.ToHexString(output.AsSpan(0, length)).ToLowerInvariant();
            foreach (int value in new[] { 0, 7, -1 })
            {
                int? expected = null;
                string? exception = null;
                try { expected = oracle(value); }
                catch (NullReferenceException error) { exception = error.GetType().FullName; }
                cases.Add(new
                {
                    module = "BootstrapLibrary", method = "Compile", fixture = method,
                    inputs = new[] { encoded, row },
                    generated = new { capacity, arguments = new[] { value }, hex, exceptionType = exception },
                    expected, exception
                });
            }
        }
    }
}
