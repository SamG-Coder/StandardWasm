#nullable enable

using System.Buffers.Binary;
using System.Reflection.Emit;
using BootstrapLibrary;
using ForeignFieldLeaf;
using ForeignFieldOwner;
using ForeignFieldTypes;

internal static class ForeignFieldTypeCases
{
    internal static void Add(List<object> cases)
    {
        const int capacity = 32768;
        byte[] entry = File.ReadAllBytes(typeof(Kernels).Assembly.Location);
        byte[] owner = File.ReadAllBytes(typeof(Cell).Assembly.Location);
        byte[] leaf = File.ReadAllBytes(typeof(Node).Assembly.Location);
        byte[] bundle = BundleCases.Pack(entry, owner, leaf);
        byte[] reordered = BundleCases.Pack(entry, leaf, owner);
        object input = Encode(bundle);

        byte[] run = Manifest(nameof(Kernels.Run));
        string runHex = Compile(bundle, run);
        if (Compile(reordered, run) != runHex)
            throw new InvalidOperationException("Reordering the type owners changed the Wasm module.");
        foreach (int value in new[] { int.MinValue, -7, 0, 17, int.MaxValue })
            Record("foreign field type repeated reference", input, run, runHex, [value], () => Kernels.Run(value));
        Record("foreign field type reordered", Encode(reordered), run, runHex, [17], () => Kernels.Run(17));

        byte[] defaults = Manifest(nameof(Kernels.DefaultFields));
        Record("foreign field type null defaults", input, defaults, Compile(bundle, defaults), [], Kernels.DefaultFields);
        byte[] read = Manifest(nameof(Kernels.NullRead));
        Record("foreign field type null dereference", input, read, Compile(bundle, read), [], Kernels.NullRead);

        Reject("foreign field type missing owner", BundleCases.Pack(entry, owner), -4);
        Reject("foreign field type ambiguous owner", BundleCases.Pack(entry, owner, leaf, leaf), -5);

        void Reject(string fixture, byte[] dlls, int expected)
        {
            var method = typeof(Kernels).GetMethod(nameof(Kernels.NullRead))!;
            int offset = IlOffsets.Find(method.GetMethodBody()!.GetILAsByteArray()!, OpCodes.Newobj);
            int[] expectedLocation = [0, method.MetadataToken & 0xffffff, offset];
            int[] diagnostic = CompilerExports.CreateDiagnostic();
            var output = Enumerable.Repeat((byte)0xa5, capacity).ToArray();
            int status = CompilerExports.CompileBundleDetailed(dlls, read, output, diagnostic);
            if (offset < 0 || status != expected || !diagnostic.SequenceEqual(expectedLocation) ||
                output.Any(value => value != 0xa5))
                throw new InvalidOperationException($"{fixture} returned {status} at [{string.Join(", ", diagnostic)}]");
            cases.Add(new
            {
                module = "BootstrapLibrary", method = "CompileBundle", fixture,
                inputs = new[] { Encode(dlls), Encode(read) },
                generated = new { capacity, bundle = true, arguments = Array.Empty<int>(), reject = true, diagnostic },
                expected
            });
        }

        void Record(string fixture, object dlls, byte[] manifest, string hex, int[] arguments, Func<int> oracle)
        {
            int? expected = null;
            string? exception = null;
            try { expected = oracle(); }
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
            int[] diagnostic = CompilerExports.CreateDiagnostic();
            int length = CompilerExports.CompileBundleDetailed(dlls, manifest, output, diagnostic);
            if (length <= 0)
                throw new InvalidOperationException($"Foreign field type compilation returned {length} at [{string.Join(", ", diagnostic)}]");
            return Convert.ToHexString(output.AsSpan(0, length)).ToLowerInvariant();
        }
    }

    private static byte[] Manifest(string method)
    {
        byte[] manifest = new byte[11];
        BinaryPrimitives.WriteInt32LittleEndian(manifest, typeof(Kernels).GetMethod(method)!.MetadataToken & 0xffffff);
        manifest[4] = 3;
        "run"u8.CopyTo(manifest.AsSpan(8));
        return manifest;
    }

    private static object Encode(byte[] bytes) => new { bytes = Array.ConvertAll(bytes, value => (int)value) };
}
