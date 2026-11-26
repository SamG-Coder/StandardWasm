using BootstrapLibrary;
using InterfaceDispatchDemo;
using ReadContract;

internal static class InterfaceDispatchCases
{
    internal static void Add(List<object> cases)
    {
        const int capacity = 65536;
        byte[] bundle = BundleCases.Pack(File.ReadAllBytes(typeof(Kernels).Assembly.Location),
            File.ReadAllBytes(typeof(IReadable).Assembly.Location));
        object encodedBundle = Encode(bundle);
        var output = new byte[capacity];
        int[] location = CompilerExports.CreateDiagnostic();

        Check(nameof(Kernels.ReadResource), [[-1], [0], [1], [2], [3], [4], [5]], arguments => Kernels.ReadResource(arguments[0]));
        Check(nameof(Kernels.ReadAlternate), [[]], _ => Kernels.ReadAlternate());

        void Check(string method, int[][] inputsForOracle, Func<int[], int> oracle)
        {
            byte[] entries = BundleCases.Manifest(typeof(Kernels), method);
            int size = CompilerExports.CompileBundleDetailed(bundle, entries, output, location);
            if (size <= 0)
                throw new InvalidOperationException($"{method} returned {size} at [{string.Join(", ", location)}].");
            string hex = Convert.ToHexString(output.AsSpan(0, size)).ToLowerInvariant();
            object[] inputs = [encodedBundle, Encode(entries)];
            foreach (int[] arguments in inputsForOracle)
            {
                int? expected = null;
                string? exception = null;
                try { expected = oracle(arguments); }
                catch (NullReferenceException error) { exception = error.GetType().FullName; }
                cases.Add(new
                {
                    module = "BootstrapLibrary", method = "CompileBundle", fixture = "interface dispatch " + method,
                    inputs, generated = new { capacity, bundle = true, arguments, hex, exceptionType = exception },
                    expected, exception
                });
            }
        }
    }

    private static object Encode(byte[] bytes) => new { bytes = Array.ConvertAll(bytes, value => (int)value) };
}
