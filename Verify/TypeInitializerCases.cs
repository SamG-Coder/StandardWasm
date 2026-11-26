using BootstrapLibrary;
using TypeInitializerDependency;
using TypeInitializerFixture;

// Stage 129 (decision 0057). These kernels return the same value however often the
// in-process CLR runs them, so the shared CLR statics here cannot skew the oracle.
// Order-sensitive and failure cases run in the stage-129 runner, which gives each
// kernel a fresh AssemblyLoadContext and Wasm instance.
internal static class TypeInitializerCases
{
    internal static void Add(List<object> cases)
    {
        const int capacity = 65536;
        byte[] bundle = BundleCases.Pack(File.ReadAllBytes(typeof(Kernels).Assembly.Location),
            File.ReadAllBytes(typeof(Registry).Assembly.Location));
        object encodedBundle = Encode(bundle);
        var output = new byte[capacity];
        int[] location = CompilerExports.CreateDiagnostic();

        Check(nameof(Kernels.BeforeFieldInitOnce), Kernels.BeforeFieldInitOnce);
        Check(nameof(Kernels.BundledDependency), Kernels.BundledDependency);
        Check(nameof(Kernels.DistinctObjects), Kernels.DistinctObjects);
        Check(nameof(Kernels.StaticFieldAlias), Kernels.StaticFieldAlias);
        Check(nameof(Kernels.NotNominalClass), Kernels.NotNominalClass);

        void Check(string method, Func<int> oracle)
        {
            byte[] entries = BundleCases.Manifest(typeof(Kernels), method);
            int size = CompilerExports.CompileBundleDetailed(bundle, entries, output, location);
            if (size <= 0)
                throw new InvalidOperationException($"{method} returned {size} at [{string.Join(", ", location)}].");
            string hex = Convert.ToHexString(output.AsSpan(0, size)).ToLowerInvariant();
            cases.Add(new
            {
                module = "BootstrapLibrary", method = "CompileBundle", fixture = "type initializers " + method,
                inputs = new[] { encodedBundle, Encode(entries) },
                generated = new { capacity, bundle = true, arguments = Array.Empty<int>(), hex },
                expected = oracle()
            });
        }
    }

    private static object Encode(byte[] bytes) => new { bytes = Array.ConvertAll(bytes, value => (int)value) };
}
