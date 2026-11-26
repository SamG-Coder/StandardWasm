#nullable enable

using System.Buffers.Binary;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using BootstrapLibrary;
using LinkedNominal;

internal static class LinkedNominalCases
{
    internal static void Add(List<object> cases)
    {
        const int capacity = 32768;
        string path = typeof(Kernels).Assembly.Location;
        string directory = Path.GetDirectoryName(path)!;
        byte[] entry = File.ReadAllBytes(path);
        byte[] bridge = File.ReadAllBytes(Path.Combine(directory, "NominalBridge.dll"));
        byte[] leaf = File.ReadAllBytes(Path.Combine(directory, "NominalLeaf.dll"));
        if (CellRow(bridge) != CellRow(leaf))
            throw new InvalidOperationException("Expected identical TypeDef rows for the two Cell classes");

        byte[] bundle = BundleCases.Pack(entry, bridge, leaf);
        byte[] reordered = BundleCases.Pack(entry, leaf, bridge);
        object encoded = Encode(bundle);
        byte[] runManifest = Manifest(nameof(Kernels.Run));
        string runHex = Compile(bundle, runManifest);
        if (Compile(reordered, runManifest) != runHex)
            throw new InvalidOperationException("Dependency order altered the linked nominal module");

        foreach (var (value, delta) in new[] { (5, 3), (0, 0), (-7, 12), (int.MaxValue, 1), (int.MinValue, -1) })
            Check("nominal overloads", encoded, runManifest, runHex, [value, delta], () => Kernels.Run(value, delta));
        Check("reordered nominal dependencies", Encode(reordered), runManifest, runHex, [5, 3], () => Kernels.Run(5, 3));

        byte[] identityManifest = Manifest(nameof(Kernels.Identity));
        string identityHex = Compile(bundle, identityManifest);
        foreach (int value in new[] { -1, 0, 42, int.MaxValue })
            Check("nominal identity and mutation", encoded, identityManifest, identityHex, [value], () => Kernels.Identity(value));

        byte[] nullManifest = Manifest(nameof(Kernels.NullRead));
        Check("nominal null parameter", encoded, nullManifest, Compile(bundle, nullManifest),
            [null], () => Kernels.NullRead(null!));

        Reject("missing class owner", BundleCases.Pack(entry, bridge), -4);
        Reject("ambiguous class owner", BundleCases.Pack(entry, bridge, leaf, leaf), -5);

        void Reject(string fixture, byte[] dlls, int expected)
        {
            var output = Enumerable.Repeat((byte)0xa5, capacity).ToArray();
            int status = CompilerExports.CompileBundle(dlls, runManifest, output);
            if (status != expected || output.Any(value => value != 0xa5))
                throw new InvalidOperationException($"{fixture} returned {status}, expected {expected}");
            cases.Add(new
            {
                module = "BootstrapLibrary", method = "CompileBundle", fixture,
                inputs = new[] { Encode(dlls), Encode(runManifest) },
                generated = new { capacity, bundle = true, arguments = Array.Empty<int>(), reject = true },
                expected
            });
        }

        void Check(string fixture, object dlls, byte[] manifest, string hex, object?[] arguments, Func<int> run)
        {
            int? expected = null;
            string? exception = null;
            try { expected = run(); }
            catch (NullReferenceException error) { exception = error.GetType().FullName; }
            cases.Add(new
            {
                module = "BootstrapLibrary", method = "CompileBundle", fixture,
                inputs = new[] { dlls, Encode(manifest) },
                generated = new { capacity, bundle = true, arguments, hex, exceptionType = exception },
                expected, exception
            });
        }

        string Compile(byte[] dlls, byte[] manifest)
        {
            var output = new byte[capacity];
            var location = CompilerExports.CreateDiagnostic();
            int length = CompilerExports.CompileBundleDetailed(dlls, manifest, output, location);
            if (length <= 0)
                throw new InvalidOperationException($"Nominal signature compilation returned {length} at [{string.Join(", ", location)}]");
            return Convert.ToHexString(output.AsSpan(0, length)).ToLowerInvariant();
        }
    }

    private static int CellRow(byte[] image)
    {
        using var pe = new PEReader(new MemoryStream(image));
        var metadata = pe.GetMetadataReader();
        var handle = metadata.TypeDefinitions.Single(type =>
        {
            var definition = metadata.GetTypeDefinition(type);
            return metadata.GetString(definition.Name) == "Cell" &&
                metadata.GetString(definition.Namespace) == "NominalTypes";
        });
        return System.Reflection.Metadata.Ecma335.MetadataTokens.GetRowNumber(handle);
    }

    private static byte[] Manifest(string name)
    {
        byte[] manifest = new byte[11];
        BinaryPrimitives.WriteInt32LittleEndian(manifest, typeof(Kernels).GetMethod(name)!.MetadataToken & 0xffffff);
        manifest[4] = 3;
        "run"u8.CopyTo(manifest.AsSpan(8));
        return manifest;
    }

    private static object Encode(byte[] bytes) => new { bytes = Array.ConvertAll(bytes, value => (int)value) };
}
