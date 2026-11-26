using System.Reflection.Emit;
using BootstrapLibrary;
using RuntimeTypeDemo;
using RuntimeTypeDependency;

internal static class RuntimeTypeCases
{
    internal static void Add(List<object> cases)
    {
        const int capacity = 65536;
        byte[] image = File.ReadAllBytes(typeof(Kernels).Assembly.Location);
        byte[] peer = File.ReadAllBytes(typeof(ExternalTypes).Assembly.Location);
        byte[] bundle = BundleCases.Pack(image, peer);
        object encoded = Encode(bundle);
        object standalone = Encode(image);
        var output = new byte[capacity];
        int[] location = CompilerExports.CreateDiagnostic();

        Check(nameof(Kernels.Local), Kernels.Local);
        Check(nameof(Kernels.RuntimeClass), Kernels.RuntimeClass, [-1, 0, 1, 2]);
        Check(nameof(Kernels.Linked), Kernels.Linked);
        Check(nameof(Kernels.DefaultHandle), Kernels.DefaultHandle);
        Check(nameof(Kernels.WithoutInitializer), Kernels.WithoutInitializer);
        Check(nameof(Kernels.CompareHostObject), value => Kernels.CompareHostObject(value), [0, 1, 2]);

        if (Kernels.ConstructedType() != 1) throw new InvalidOperationException("CLR constructed type fixture changed.");
        Reject(nameof(Kernels.ConstructedType), bundle, encoded, OpCodes.Ldtoken, -2);
        byte[] missing = BundleCases.Pack(image);
        Reject(nameof(Kernels.RequiresPeer), missing, Encode(missing), OpCodes.Ldtoken, -4);
        if (Kernels.RequiresTypeTest() != 1) throw new InvalidOperationException("CLR type-test fixture changed.");
        Reject(nameof(Kernels.RequiresTypeTest), bundle, encoded, OpCodes.Isinst, -2);

        if (Kernels.BoxHandle() != 1) throw new InvalidOperationException("CLR handle boxing fixture changed.");
        Reject(nameof(Kernels.BoxHandle), bundle, encoded, OpCodes.Box, -2);

        CheckStandalone(nameof(Kernels.Local), Kernels.Local);
        CheckStandalone(nameof(Kernels.DefaultHandle), Kernels.DefaultHandle);
        CheckStandalone(nameof(Kernels.WithoutInitializer), Kernels.WithoutInitializer);
        RuntimeTypeScopeCases.Add(cases);

        void Check(string name, Func<int, int> oracle, int[] values = null)
        {
            byte[] entries = BundleCases.Manifest(typeof(Kernels), name);
            int length = CompilerExports.CompileBundleDetailed(bundle, entries, output, location);
            if (length < 1)
                throw new InvalidOperationException($"{name} returned {length} at [{string.Join(", ", location)}].");
            string hex = Convert.ToHexString(output.AsSpan(0, length)).ToLowerInvariant();
            foreach (int value in values ?? [int.MinValue, -1, 0, 5, int.MaxValue])
                cases.Add(new
                {
                    module = "BootstrapLibrary", method = "CompileBundle", fixture = "runtime type " + name,
                    inputs = new[] { encoded, Encode(entries) },
                    generated = new { capacity, bundle = true, arguments = new[] { value }, hex },
                    expected = oracle(value)
                });
        }

        void CheckStandalone(string name, Func<int, int> oracle)
        {
            int row = typeof(Kernels).GetMethod(name)!.MetadataToken & 0xffffff;
            foreach (bool detailed in new[] { false, true })
            {
                int length = detailed
                    ? CompilerExports.CompileDetailed(image, row, output, location)
                    : CompilerExports.Compile(image, row, output);
                int[] diagnostic = detailed ? [-1, 0, -1] : null;
                if (length <= 0 || detailed && !location.SequenceEqual(diagnostic))
                    throw new InvalidOperationException($"Standalone {name} returned {length} at [{string.Join(", ", location)}].");
                string hex = Convert.ToHexString(output.AsSpan(0, length)).ToLowerInvariant();
                string method = detailed ? "CompileDetailed" : "Compile";
                foreach (int value in new[] { int.MinValue, -1, 0, 5, int.MaxValue })
                    cases.Add(new
                    {
                        module = "BootstrapLibrary", method, fixture = $"runtime type standalone {name}",
                        inputs = new[] { standalone, (object)row },
                        generated = new { capacity, arguments = new[] { value }, hex, diagnostic },
                        expected = oracle(value)
                    });
            }
        }

        void Reject(string name, byte[] input, object packed, OpCode operation, int expected)
        {
            var method = typeof(Kernels).GetMethod(name)!;
            int offset = IlOffsets.Find(method.GetMethodBody()!.GetILAsByteArray()!, operation);
            int[] expectedLocation = [0, method.MetadataToken & 0xffffff, offset];
            byte[] entries = BundleCases.Manifest(typeof(Kernels), name);
            Array.Fill(output, (byte)0xa5);
            int status = CompilerExports.CompileBundleDetailed(input, entries, output, location);
            if (offset < 0 || status != expected || !location.SequenceEqual(expectedLocation) || output.Any(value => value != 0xa5))
                throw new InvalidOperationException($"{name} rejection returned {status} at [{string.Join(", ", location)}], expected {expected} at [{string.Join(", ", expectedLocation)}].");
            cases.Add(new
            {
                module = "BootstrapLibrary", method = "CompileBundle", fixture = "runtime type reject " + name,
                inputs = new[] { packed, Encode(entries) },
                generated = new { capacity, bundle = true, arguments = Array.Empty<int>(), reject = true, diagnostic = expectedLocation },
                expected = status
            });
        }
    }

    private static object Encode(byte[] bytes) => new { bytes = Array.ConvertAll(bytes, value => (int)value) };
}
