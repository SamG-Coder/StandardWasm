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
        RejectCall(nameof(Kernels.ReachedLocal), null, Kernels.ReachedLocal);
        RejectCall(nameof(Kernels.ReachedDependency), bundle, Kernels.ReachedDependency);

        if (ModuleInitFixture.Kernel.Initialized != 1)
            throw new InvalidOperationException("The CLR did not run the module initializer.");
        var module = typeof(ModuleInitFixture.Kernel).Assembly;
        int row = typeof(ModuleInitFixture.Kernel).GetMethod(nameof(ModuleInitFixture.Kernel.Read))!.MetadataToken & 0xffffff;
        Reject("module initializer", File.ReadAllBytes(module.Location), row, null, [0, 0, -1]);
        Reject("supplied dependency module initializer", source, Row(nameof(Kernels.Safe)),
            BundleCases.Pack(source, File.ReadAllBytes(module.Location)), [1, 0, -1]);

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

        void RejectCall(string name, byte[]? packed, Func<int, int> oracle)
        {
            try { _ = oracle(1); }
            catch (TypeInitializationException error) when (error.InnerException is FormatException)
            {
                var method = typeof(Kernels).GetMethod(name)!;
                int methodRow = method.MetadataToken & 0xffffff;
                int offset = IlOffsets.Find(method.GetMethodBody()!.GetILAsByteArray()!, OpCodes.Call);
                Reject(name, source, methodRow, packed, [0, methodRow, offset]);
                return;
            }
            throw new InvalidOperationException($"The CLR did not require the initializer in {name}.");
        }

        void Reject(string name, byte[] image, int methodRow, byte[]? packed, int[] expected)
        {
            var output = Enumerable.Repeat((byte)0xa5, capacity).ToArray();
            int[] location = CompilerExports.CreateDiagnostic();
            int result = packed == null ? CompilerExports.CompileDetailed(image, methodRow, output, location) :
                CompilerExports.CompileBundleDetailed(packed, Manifest(methodRow), output, location);
            if (result != -2 || !location.SequenceEqual(expected) || output.Any(value => value != 0xa5))
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
