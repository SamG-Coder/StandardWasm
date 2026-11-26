using BootstrapLibrary;
using WideLayoutDemo;
using WideLayoutDependency;

internal static class WideLayoutCases
{
    internal static void Add(List<object> cases)
    {
        const int capacity = 65536, tooSmall = 256;
        var entry = typeof(Kernels).GetMethod(nameof(Kernels.Run))!;
        if (typeof(LayoutTable).GetFields().Length != 311)
            throw new InvalidOperationException("Unexpected reachable type fixture size.");
        byte[] bundle = BundleCases.Pack(File.ReadAllBytes(entry.DeclaringType!.Assembly.Location),
            File.ReadAllBytes(typeof(LayoutTable).Assembly.Location));
        byte[] manifest = BootstrapCases.Manifest([("run", entry.MetadataToken & 0xffffff)]);
        object[] inputs = [Encode(bundle), Encode(manifest)];
        byte[] output = new byte[capacity];
        int[] location = CompilerExports.CreateDiagnostic();
        int size = CompilerExports.CompileBundleDetailed(bundle, manifest, output, location);
        if (size <= 0)
            throw new InvalidOperationException($"Wide layout compilation returned {size} at [{string.Join(", ", location)}].");
        string hex = Convert.ToHexString(output.AsSpan(0, size)).ToLowerInvariant();
        foreach (int value in new[] { int.MinValue, -17, 0, 5, int.MaxValue })
            foreach (int mode in new[] { 0, 1 })
                cases.Add(new
                {
                    module = "BootstrapLibrary", method = "CompileBundle", fixture = "wide managed layouts",
                    inputs, generated = new { capacity, bundle = true, arguments = new[] { value, mode }, hex },
                    expected = Kernels.Run(value, mode)
                });

        byte[] small = Enumerable.Repeat((byte)0xa5, tooSmall).ToArray();
        if (CompilerExports.CompileBundle(bundle, manifest, small) != -3 || small.Any(value => value != 0xa5))
            throw new InvalidOperationException("The small output failed to preserve the original buffer.");
        cases.Add(new
        {
            module = "BootstrapLibrary", method = "CompileBundle", fixture = "wide layouts output capacity",
            inputs, generated = new { capacity = tooSmall, bundle = true, arguments = Array.Empty<int>(), reject = true },
            expected = -3
        });
    }

    private static object Encode(byte[] bytes) => new { bytes = Array.ConvertAll(bytes, value => (int)value) };
}
