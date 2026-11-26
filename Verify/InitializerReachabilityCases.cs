#nullable enable

using System.Buffers.Binary;
using System.Reflection.Emit;
using BootstrapLibrary;
using InitializerDependency;
using InitializerReachability;

internal static class InitializerReachabilityCases
{
    internal static void Add(List<object> cases)
    {
        const int capacity = 16384;
        byte[] source = File.ReadAllBytes(typeof(Kernels).Assembly.Location);
        byte[] dependency = File.ReadAllBytes(typeof(Independent).Assembly.Location);
        byte[] bundle = BundleCases.Pack(source, dependency);
        Check(nameof(Kernels.Safe), null, Kernels.Safe);
        Check(nameof(Kernels.SafeBundle), bundle, Kernels.SafeBundle);
        // Stage 129 (decision 0057): a reached initializer is compiled. Its unsupported
        // int.Parse call is diagnosed inside the .cctor, in the module that declares it:
        // unsupported (-2) in a single image, and an unbundled assembly (-4) in a bundle.
        RejectCall(nameof(Kernels.ReachedLocal), null, Kernels.ReachedLocal, typeof(LocalInitializer), 0, -2);
        RejectCall(nameof(Kernels.ReachedDependency), bundle, Kernels.ReachedDependency, typeof(NeedsInitializer), 1, -4);

        if (ModuleInitFixture.Kernel.Initialized != 1)
            throw new InvalidOperationException("The CLR did not run the module initializer.");
        var module = typeof(ModuleInitFixture.Kernel).Assembly;
        int row = typeof(ModuleInitFixture.Kernel).GetMethod(nameof(ModuleInitFixture.Kernel.Read))!.MetadataToken & 0xffffff;
        Reject("module initializer", File.ReadAllBytes(module.Location), row, null, [0, 0, -1], -2);
        Reject("supplied dependency module initializer", source, Row(nameof(Kernels.Safe)),
            BundleCases.Pack(source, File.ReadAllBytes(module.Location)), [1, 0, -1], -2);

        void Check(string name, byte[]? packed, Func<int, int> oracle)
        {
            int row = Row(name);
            var output = new byte[capacity];
            int size = packed == null ? CompilerExports.Compile(source, row, output) :
                CompilerExports.CompileBundle(packed, Manifest(row), output);
            if (size <= 0) throw new InvalidOperationException($"{name} returned {size}");
            string hex = Convert.ToHexString(output.AsSpan(0, size)).ToLowerInvariant();
            foreach (int value in new[] { int.MinValue, 0, 11, int.MaxValue })
                cases.Add(new
                {
                    module = "BootstrapLibrary", method = packed == null ? "Compile" : "CompileBundle", fixture = name,
                    inputs = Inputs(source, row, packed),
                    generated = new { capacity, bundle = packed != null, arguments = new[] { value }, hex },
                    expected = oracle(value)
                });
        }

        void RejectCall(string name, byte[]? packed, Func<int, int> oracle, Type initialized, int module, int status)
        {
            try { _ = oracle(1); }
            catch (TypeInitializationException error) when (error.InnerException is FormatException)
            {
                int methodRow = typeof(Kernels).GetMethod(name)!.MetadataToken & 0xffffff;
                var initializer = initialized.TypeInitializer!;
                int initializerRow = initializer.MetadataToken & 0xffffff;
                int offset = IlOffsets.Find(initializer.GetMethodBody()!.GetILAsByteArray()!, OpCodes.Call);
                Reject(name, source, methodRow, packed, [module, initializerRow, offset], status);
                return;
            }
            throw new InvalidOperationException($"The CLR did not require the initializer in {name}.");
        }

        void Reject(string name, byte[] image, int methodRow, byte[]? packed, int[] expected, int status)
        {
            var output = Enumerable.Repeat((byte)0xa5, capacity).ToArray();
            int[] location = CompilerExports.CreateDiagnostic();
            int result = packed == null ? CompilerExports.CompileDetailed(image, methodRow, output, location) :
                CompilerExports.CompileBundleDetailed(packed, Manifest(methodRow), output, location);
            if (result != status || !location.SequenceEqual(expected) || output.Any(value => value != 0xa5))
                throw new InvalidOperationException($"{name} returned {result} at [{string.Join(", ", location)}]");
            cases.Add(new
            {
                module = "BootstrapLibrary", method = packed == null ? "Compile" : "CompileBundle", fixture = name,
                inputs = Inputs(image, methodRow, packed),
                generated = new { capacity, bundle = packed != null, arguments = Array.Empty<int>(), reject = true, diagnostic = expected },
                expected = result
            });
        }
    }

    private static int Row(string name) => typeof(Kernels).GetMethod(name)!.MetadataToken & 0xffffff;

    private static object Encode(byte[] image) => new { bytes = Array.ConvertAll(image, value => (int)value) };

    private static object[] Inputs(byte[] image, int row, byte[]? packed) =>
        packed == null ? [Encode(image), row] : [Encode(packed), Encode(Manifest(row))];

    private static byte[] Manifest(int row)
    {
        byte[] manifest = new byte[11];
        BinaryPrimitives.WriteInt32LittleEndian(manifest, row);
        manifest[4] = 3;
        "run"u8.CopyTo(manifest.AsSpan(8));
        return manifest;
    }
}
