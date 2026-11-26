#nullable enable

using System.Buffers.Binary;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using BootstrapLibrary;
using ExceptionBridge;
using ExceptionLeaf;
using LinkedExceptions;

internal static class LinkedExceptionCases
{
    internal static void Add(List<object> cases)
    {
        const int capacity = 16384;
        byte[] entry = File.ReadAllBytes(typeof(Kernels).Assembly.Location);
        byte[] bridge = File.ReadAllBytes(typeof(Bridge).Assembly.Location);
        byte[] leaf = File.ReadAllBytes(typeof(Leaf).Assembly.Location);
        byte[] bundle = BundleCases.Pack(entry, bridge, leaf);
        byte[] reordered = BundleCases.Pack(entry, leaf, bridge);

        byte[] runManifest = Manifest(nameof(Kernels.Run));
        string runHex = Compile(bundle, runManifest);
        if (Compile(reordered, runManifest) != runHex)
            throw new InvalidOperationException("Dependency order changed the compiled exception module");

        (int Value, int Divisor, int Cleanup)[] values =
            [(18, 3, 4), (18, 0, 4), (int.MinValue, -1, 4), (18, 0, int.MaxValue),
             (18, 3, int.MaxValue), (int.MaxValue, 1, -1), (int.MinValue, 1, -1)];
        foreach (var (value, divisor, cleanup) in values)
            Check("linked cleanup and catch", bundle, runManifest, runHex, [value, divisor, cleanup],
                () => Kernels.Run(value, divisor, cleanup));
        Check("reordered linked cleanup", reordered, runManifest, runHex, [18, 0, 4],
            () => Kernels.Run(18, 0, 4));

        byte[] escapeManifest = Manifest(nameof(Kernels.Propagate));
        string escapeHex = Compile(bundle, escapeManifest);
        foreach (var (value, divisor) in new[] { (9, 3), (9, 0), (int.MinValue, -1) })
            Check("linked exception propagation", bundle, escapeManifest, escapeHex, [value, divisor],
                () => Kernels.Propagate(value, divisor));

        int row = typeof(Bridge).GetMethod(nameof(Bridge.Work))!.MetadataToken & 0xffffff;
        using var pe = new PEReader(new MemoryStream(bridge));
        int rva = pe.GetMetadataReader().GetMethodDefinition(MetadataTokens.MethodDefinitionHandle(row)).RelativeVirtualAddress;
        int il = IlOffsets.Find(pe.GetMethodBody(rva).GetILBytes()!, OpCodes.Endfinally);
        if (il < 0) throw new InvalidOperationException("The bridge method did not emit endfinally");
        var section = pe.PEHeaders.SectionHeaders[pe.PEHeaders.GetContainingSectionIndex(rva)];
        int start = section.PointerToRawData + rva - section.VirtualAddress;
        int header = (bridge[start + 1] >> 4) * 4;
        byte[] malformed = (byte[])bridge.Clone();
        malformed[start + header + il] = (byte)OpCodes.Ret.Value;
        Reject(BundleCases.Pack(entry, malformed, leaf), 1);
        Reject(BundleCases.Pack(entry, leaf, malformed), 2);

        void Reject(byte[] changed, int owner)
        {
            var output = Enumerable.Repeat((byte)0xa5, capacity).ToArray();
            var diagnostic = CompilerExports.CreateDiagnostic();
            int status = CompilerExports.CompileBundleDetailed(changed, runManifest, output, diagnostic);
            if (status != -1 || !diagnostic.SequenceEqual(new[] { owner, row, il }) ||
                output.Any(value => value != 0xa5))
                throw new InvalidOperationException($"Malformed dependency returned {status} at [{string.Join(", ", diagnostic)}]");
            cases.Add(new
            {
                module = "BootstrapLibrary", method = "CompileBundle", fixture = "malformed linked cleanup",
                inputs = new[] { Encode(changed), Encode(runManifest) },
                generated = new { capacity, bundle = true, arguments = Array.Empty<int>(), reject = true, diagnostic },
                expected = status
            });
        }

        string Compile(byte[] dlls, byte[] manifest)
        {
            var output = new byte[capacity];
            int length = CompilerExports.CompileBundle(dlls, manifest, output);
            if (length <= 0) throw new InvalidOperationException($"Linked exception compilation returned {length}");
            return Convert.ToHexString(output.AsSpan(0, length)).ToLowerInvariant();
        }

        void Check(string fixture, byte[] dlls, byte[] manifest, string hex, int[] arguments, Func<int> oracle)
        {
            int? expected = null;
            string? exception = null;
            try { expected = oracle(); }
            catch (ArithmeticException error) { exception = error.GetType().FullName; }
            cases.Add(new
            {
                module = "BootstrapLibrary", method = "CompileBundle", fixture,
                inputs = new[] { Encode(dlls), Encode(manifest) },
                generated = new { capacity, bundle = true, arguments, hex, exceptionType = exception },
                expected, exception
            });
        }
    }

    private static byte[] Manifest(string method)
    {
        byte[] manifest = new byte[11];
        int row = typeof(Kernels).GetMethod(method)!.MetadataToken & 0xffffff;
        BinaryPrimitives.WriteInt32LittleEndian(manifest, row);
        manifest[4] = 3;
        "run"u8.CopyTo(manifest.AsSpan(8));
        return manifest;
    }

    private static object Encode(byte[] bytes) => new { bytes = Array.ConvertAll(bytes, value => (int)value) };
}
