#nullable enable

using System.Buffers.Binary;
using System.Reflection;
using BootstrapLibrary;

internal static class ModuleStartupCases
{
    private const int Capacity = 65536;

    internal static void Add(List<object> cases)
    {
        byte[] empty = Image(typeof(EmptyModuleStartup.Kernels));
        byte[] linked = Image(typeof(EmptyModuleConsumer.Kernels));
        AddPositive("empty module startup", empty, null, EmptyModuleStartup.Kernels.Twice);
        AddPositive("linked empty module startup", linked, BundleCases.Pack(linked, empty),
            EmptyModuleConsumer.Kernels.AddOneAfterTwice);

        Func<int, int> readTriggered = ModuleStartupTypeTrigger.Kernels.Read;
        if (readTriggered(5) != 12)
            throw new InvalidOperationException("The CLR did not run the helper's declaring type initializer.");
        byte[] triggered = Image(typeof(ModuleStartupTypeTrigger.Kernels));
        var output = Enumerable.Repeat((byte)0xa5, Capacity).ToArray();
        int row = Row(readTriggered.Method);
        int[] diagnostic = [0, 0, -1];
        int[] actual = CompilerExports.CreateDiagnostic();
        int result = CompilerExports.CompileDetailed(triggered, row, output, actual);
        if (result != -2 || !actual.SequenceEqual(diagnostic) || output.Any(value => value != 0xa5))
            throw new InvalidOperationException($"Type initializer guard returned {result} at [{string.Join(",", actual)}].");
        cases.Add(new
        {
            module = "BootstrapLibrary", method = "Compile", fixture = "empty helper with a required type initializer",
            inputs = new object[] { Encode(triggered), row },
            generated = new { capacity = Capacity, arguments = Array.Empty<int>(), reject = true, diagnostic },
            expected = result
        });

        void AddPositive(string name, byte[] source, byte[]? bundle, Func<int, int> oracle)
        {
            var bytes = new byte[Capacity];
            int methodRow = Row(oracle.Method);
            byte[] manifest = Manifest(methodRow);
            int size = bundle == null
                ? CompilerExports.Compile(source, methodRow, bytes)
                : CompilerExports.CompileBundle(bundle, manifest, bytes);
            if (size <= 0) throw new InvalidOperationException($"{name} returned {size}.");
            string hex = Convert.ToHexString(bytes.AsSpan(0, size)).ToLowerInvariant();
            object[] inputs = bundle == null ? [Encode(source), methodRow] : [Encode(bundle), Encode(manifest)];
            foreach (int input in new[] { int.MinValue, -1, 0, 21, int.MaxValue })
                cases.Add(new
                {
                    module = "BootstrapLibrary", method = bundle == null ? "Compile" : "CompileBundle", fixture = name,
                    inputs,
                    generated = new { capacity = Capacity, bundle = bundle != null, arguments = new[] { input }, hex },
                    expected = oracle(input)
                });
        }
    }

    private static int Row(MethodInfo method) => method.MetadataToken & 0xffffff;

    private static byte[] Image(Type type) => File.ReadAllBytes(type.Assembly.Location);

    private static object Encode(byte[] image) => new { bytes = Array.ConvertAll(image, value => (int)value) };

    private static byte[] Manifest(int row)
    {
        var manifest = new byte[11];
        BinaryPrimitives.WriteInt32LittleEndian(manifest, row);
        manifest[4] = 3;
        "run"u8.CopyTo(manifest.AsSpan(8));
        return manifest;
    }
}
