using System.Buffers.Binary;
using BootstrapLibrary;
using HierarchyBase;
using HierarchyDemo;
using HierarchyMiddle;

internal static class InheritedLayoutCases
{
    internal static void Add(List<object> cases)
    {
        const int capacity = 32768;
        byte[] main = File.ReadAllBytes(typeof(Kernels).Assembly.Location);
        byte[] root = File.ReadAllBytes(typeof(Root).Assembly.Location);
        byte[] middle = File.ReadAllBytes(typeof(Middle).Assembly.Location);
        byte[] bundle = BundleCases.Pack(main, root, middle);
        byte[] reordered = BundleCases.Pack(main, middle, root);
        object encoded = Encode(bundle);
        Check(nameof(Kernels.Run), Kernels.Run);
        Check(nameof(Kernels.Cast), Kernels.Cast);

        void Check(string name, Func<int, int> oracle)
        {
            int row = typeof(Kernels).GetMethod(name)!.MetadataToken & 0xffffff;
            byte[] manifest = new byte[11];
            BinaryPrimitives.WriteInt32LittleEndian(manifest, row);
            manifest[4] = 3;
            "run"u8.CopyTo(manifest.AsSpan(8));
            byte[] output = new byte[capacity], other = new byte[capacity];
            var location = CompilerExports.CreateDiagnostic();
            int size = CompilerExports.CompileBundleDetailed(bundle, manifest, output, location);
            if (size <= 0)
                throw new InvalidOperationException($"{name} returned {size} at [{string.Join(", ", location)}]");
            int otherSize = CompilerExports.CompileBundle(reordered, manifest, other);
            if (otherSize != size || !output.AsSpan(0, size).SequenceEqual(other.AsSpan(0, otherSize)))
                throw new InvalidOperationException("Dependency order changed the inherited layout module.");
            string hex = Convert.ToHexString(output.AsSpan(0, size)).ToLowerInvariant();
            foreach (int value in new[] { 5, 0, -29, int.MaxValue, int.MinValue })
                cases.Add(new
                {
                    module = "BootstrapLibrary", method = "CompileBundle", fixture = name,
                    inputs = new[] { encoded, Encode(manifest) },
                    generated = new { capacity, bundle = true, arguments = new[] { value }, hex },
                    expected = oracle(value)
                });
            byte[] missing = BundleCases.Pack(main, middle);
            Array.Fill(other, (byte)0xa5);
            int status = CompilerExports.CompileBundleDetailed(missing, manifest, other, location);
            if (status != -4 || other.Any(value => value != 0xa5))
                throw new InvalidOperationException("Missing base assembly did not preserve the output.");
            cases.Add(new
            {
                module = "BootstrapLibrary", method = "CompileBundle", fixture = name + " missing base",
                inputs = new[] { Encode(missing), Encode(manifest) },
                generated = new { capacity, bundle = true, arguments = Array.Empty<int>(), reject = true, diagnostic = location },
                expected = status
            });
        }

    }

    private static object Encode(byte[] image) => new { bytes = Array.ConvertAll(image, value => (int)value) };
}
