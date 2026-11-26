using BootstrapLibrary;
using DispatchDependency;
using RuntimeDispatchDemo;

internal static class RuntimeDispatchCases
{
    internal static void Add(List<object> cases)
    {
        const int capacity = 65536;
        byte[] bundle = BundleCases.Pack(File.ReadAllBytes(typeof(Kernels).Assembly.Location),
            File.ReadAllBytes(typeof(Item).Assembly.Location));
        object encodedBundle = Encode(bundle);
        byte[] output = new byte[capacity];
        int[] location = CompilerExports.CreateDiagnostic();

        Check(nameof(Kernels.LinkedProperty), Kernels.LinkedProperty);
        Check(nameof(Kernels.HiddenSlot), Kernels.HiddenSlot);
        Check(nameof(Kernels.DiscoveredOverride), Kernels.DiscoveredOverride);
        Check(nameof(Kernels.NullOnly), Kernels.NullOnly);

        void Check(string name, Func<int, int> oracle)
        {
            byte[] entries = BundleCases.Manifest(typeof(Kernels), name);
            int length = CompilerExports.CompileBundleDetailed(bundle, entries, output, location);
            if (length < 1)
                throw new InvalidOperationException($"{name} returned {length} at [{string.Join(", ", location)}].");
            string hex = Convert.ToHexString(output.AsSpan(0, length)).ToLowerInvariant();
            object[] inputs = [encodedBundle, Encode(entries)];
            foreach (int input in new[] { int.MinValue, -1, 0, 1, 2, int.MaxValue })
                cases.Add(new
                {
                    module = "BootstrapLibrary", method = "CompileBundle", fixture = "runtime class " + name,
                    inputs, generated = new { capacity, bundle = true, arguments = new[] { input }, hex },
                    expected = oracle(input)
                });
        }
    }

    private static object Encode(byte[] bytes) => new { bytes = Array.ConvertAll(bytes, value => (int)value) };
}
