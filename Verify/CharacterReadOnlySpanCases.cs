using BootstrapLibrary;
using CharacterReadOnlySpansFixture;

internal static class CharacterReadOnlySpanCases
{
    internal static void Add(List<object> cases)
    {
        for (int index = 0; index < 6; index++)
            Check(nameof(Kernels.CodeUnit), [index], Kernels.CodeUnit(index));

        for (int kind = 0; kind < 3; kind++)
        {
            Check(nameof(Kernels.SelectedLength), [kind], Kernels.SelectedLength(kind));
            Check(nameof(Kernels.MixedKinds), [kind], Kernels.MixedKinds(kind));
        }

        foreach (int index in new[] { int.MinValue, -1, 0, 1, 4, 5, 6, int.MaxValue })
            for (int useNull = 0; useNull < 2; useNull++)
                Check(nameof(Kernels.CheckedIndex), [index, useNull], Kernels.CheckedIndex(index, useNull));

        CrossAssembly(cases);
        FixedString(cases);
        void Check(string method, int[] inputs, int expected) =>
            cases.Add(new { module = "CharacterReadOnlySpansFixture", method, inputs, expected });
    }

    private static void CrossAssembly(List<object> cases)
    {
        const int capacity = 65536;
        byte[] bundle = BundleCases.Pack(
            File.ReadAllBytes(typeof(CrossAssemblyProbe).Assembly.Location),
            File.ReadAllBytes(typeof(SpanCompanion.Readers).Assembly.Location));
        byte[] entries = BundleCases.Manifest(typeof(CrossAssemblyProbe), nameof(CrossAssemblyProbe.CallCompanion));
        var output = new byte[capacity];
        int[] location = CompilerExports.CreateDiagnostic();
        int length = CompilerExports.CompileBundleDetailed(bundle, entries, output, location);
        if (length <= 0)
            throw new InvalidOperationException($"Cross-assembly span call returned {length} at [{string.Join(", ", location)}].");

        cases.Add(new
        {
            module = "BootstrapLibrary", method = "CompileBundle", fixture = "character and object spans across assemblies",
            inputs = new[] { Encode(bundle), Encode(entries) },
            generated = new
            {
                capacity, bundle = true, arguments = Array.Empty<int>(),
                hex = Convert.ToHexString(output.AsSpan(0, length)).ToLowerInvariant()
            },
            expected = CrossAssemblyProbe.CallCompanion()
        });
    }

    private static void FixedString(List<object> cases)
    {
        const int capacity = 65536;
        byte[] image = File.ReadAllBytes(typeof(FixedStringProbe).Assembly.Location);
        var method = typeof(FixedStringProbe).GetMethod(nameof(FixedStringProbe.Read))!;
        int row = method.MetadataToken & 0xffffff;
        byte[] output = new byte[capacity];
        int[] diagnostic = CompilerExports.CreateDiagnostic();
        int size = CompilerExports.CompileDetailed(image, row, output, diagnostic);
        if (size <= 0)
            throw new InvalidOperationException($"Fixed string returned {size} at [{string.Join(", ", diagnostic)}].");
        string hex = Convert.ToHexString(output.AsSpan(0, size)).ToLowerInvariant();
        for (int selection = 0; selection < 5; selection++)
            cases.Add(new
            {
                module = "BootstrapLibrary", method = "Compile", fixture = "fixed UTF-16 string",
                inputs = new object[] { Encode(image), row },
                generated = new { capacity, arguments = new[] { selection }, memory = true, hex },
                expected = FixedStringProbe.Read(selection)
            });
    }

    private static object Encode(byte[] bytes) => new { bytes = Array.ConvertAll(bytes, value => (int)value) };
}
