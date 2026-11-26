#nullable enable

using System.Buffers.Binary;
using System.Text;
using BootstrapLibrary;
using LinkedReferences;
using ReferenceBridge;
using ReferenceLeaf;

internal static class LinkedReferenceCases
{
    internal static void Add(List<object> cases)
    {
        const int capacity = 32768;
        byte[] entry = File.ReadAllBytes(typeof(Kernels).Assembly.Location);
        byte[] bridge = File.ReadAllBytes(typeof(Bridge).Assembly.Location);
        byte[] leaf = File.ReadAllBytes(typeof(Leaf).Assembly.Location);
        byte[] bundle = BundleCases.Pack(entry, bridge, leaf);
        byte[] reordered = BundleCases.Pack(entry, leaf, bridge);
        object encoded = Encode(bundle), encodedReordered = Encode(reordered);

        byte[] aliasManifest = Manifest(nameof(Kernels.Alias));
        string aliasHex = Compile(bundle, aliasManifest);
        if (Compile(reordered, aliasManifest) != aliasHex)
            throw new InvalidOperationException("Dependency order changed the linked array module");
        foreach (var (index, value) in new[] { (0, 7), (0, -1), (1, 255), (-1, 4), (2, 4) })
            Check(nameof(Kernels.Alias), encoded, aliasManifest, aliasHex, [index, value], () => Kernels.Alias(index, value));
        Check("reordered reference dependencies", encodedReordered, aliasManifest, aliasHex, [0, 7], () => Kernels.Alias(0, 7));

        CheckScalar(nameof(Kernels.Signedness), [-129, -128, -1, 0, 127, 128, 255, 256], Kernels.Signedness);
        CheckScalar(nameof(Kernels.ReadLiteral), [-1, 0, 4, 7, 8], Kernels.ReadLiteral);

        byte[] nullManifest = Manifest(nameof(Kernels.NullArray));
        Check(nameof(Kernels.NullArray), encoded, nullManifest, Compile(bundle, nullManifest),
            [null], () => Kernels.NullArray(null!));

        byte[] labelManifest = Manifest(nameof(Kernels.Label), nameof(Kernels.TextLength), nameof(Kernels.TextCodeUnit));
        string labelHex = Compile(bundle, labelManifest);
        foreach (var (value, variant) in new[] { (65, 0), (233, 1), (0, 0), (0x20ac, 1) })
            Check(nameof(Kernels.Label), encoded, labelManifest, labelHex, [value, variant],
                () => Kernels.Label(value, variant), "string");

        byte[] unitManifest = Manifest(nameof(Kernels.ReadLabel));
        string unitHex = Compile(bundle, unitManifest);
        foreach (int index in new[] { -1, 0, 1, 2, 3, 4 })
            Check(nameof(Kernels.ReadLabel), encoded, unitManifest, unitHex, [65, 1, index],
                () => Kernels.ReadLabel(65, 1, index));

        void CheckScalar(string method, int[] arguments, Func<int, int> oracle)
        {
            byte[] manifest = Manifest(method);
            string hex = Compile(bundle, manifest);
            foreach (int value in arguments)
                Check(method, encoded, manifest, hex, [value], () => oracle(value));
        }

        void Check(string fixture, object input, byte[] manifest, string hex, object?[] arguments,
            Func<object> oracle, string? resultType = null)
        {
            object? expected = null;
            string? exception = null;
            try { expected = oracle(); }
            catch (Exception error) when (error is IndexOutOfRangeException or NullReferenceException)
            {
                exception = error.GetType().FullName;
            }
            cases.Add(new
            {
                module = "BootstrapLibrary", method = "CompileBundle", fixture,
                inputs = new[] { input, Encode(manifest) },
                generated = new { capacity, bundle = true, arguments, hex, exceptionType = exception, resultType },
                expected, exception
            });
        }

        string Compile(byte[] dlls, byte[] manifest)
        {
            var output = new byte[capacity];
            int length = CompilerExports.CompileBundle(dlls, manifest, output);
            if (length <= 0) throw new InvalidOperationException($"Linked references returned {length}");
            return Convert.ToHexString(output.AsSpan(0, length)).ToLowerInvariant();
        }
    }

    private static byte[] Manifest(params string[] methods)
    {
        using var entries = new MemoryStream();
        for (int i = 0; i < methods.Length; i++)
        {
            int row = typeof(Kernels).GetMethod(methods[i])!.MetadataToken & 0xffffff;
            byte[] name = Encoding.ASCII.GetBytes(i == 0 ? "run" : methods[i]);
            byte[] header = new byte[8];
            BinaryPrimitives.WriteInt32LittleEndian(header, row);
            BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(4), name.Length);
            entries.Write(header);
            entries.Write(name);
        }
        return entries.ToArray();
    }

    private static object Encode(byte[] bytes) => new { bytes = Array.ConvertAll(bytes, value => (int)value) };
}
