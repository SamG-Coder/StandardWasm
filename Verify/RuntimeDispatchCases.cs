using System.Reflection;
using System.Reflection.Emit;
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

        // A class escaping to another Wasm instance can keep a mutable alias.
        // That class and its children are outside this closed-world ABI.
        byte[] exposed = BundleCases.Manifest(typeof(Kernels), nameof(Kernels.ExposeManagedReference));
        MethodInfo read = typeof(Kernels).GetMethod("Read", BindingFlags.NonPublic | BindingFlags.Static)!;
        int call = IlOffsets.Find(read.GetMethodBody()!.GetILAsByteArray()!, OpCodes.Callvirt);
        int[] expectedLocation = [0, read.MetadataToken & 0xffffff, call];
        Array.Fill(output, (byte)0xa5);
        int rejected = CompilerExports.CompileBundleDetailed(bundle, exposed, output, location);
        if (call < 0 || rejected != -2 || !location.SequenceEqual(expectedLocation) || output.Any(value => value != 0xa5))
            throw new InvalidOperationException($"Escaping a managed class returned {rejected} at [{string.Join(", ", location)}].");
        cases.Add(new
        {
            module = "BootstrapLibrary", method = "CompileBundle", fixture = "runtime class exported managed reference",
            inputs = new[] { encodedBundle, Encode(exposed) },
            generated = new { capacity, bundle = true, arguments = Array.Empty<int>(), reject = true, diagnostic = expectedLocation },
            expected = rejected
        });

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
