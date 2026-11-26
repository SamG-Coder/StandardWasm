#nullable enable

using System.Buffers.Binary;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using BootstrapLibrary;
using LinkedFields;

internal static class LinkedFieldCases
{
    internal static void Add(List<object> cases)
    {
        const int capacity = 32768;
        string entryPath = typeof(Kernels).Assembly.Location;
        string directory = Path.GetDirectoryName(entryPath)!;
        byte[] entry = File.ReadAllBytes(entryPath);
        byte[] left = File.ReadAllBytes(Path.Combine(directory, "FieldLeft.dll"));
        byte[] right = File.ReadAllBytes(Path.Combine(directory, "FieldRight.dll"));
        byte[] bundle = BundleCases.Pack(entry, left, right);
        byte[] reordered = BundleCases.Pack(entry, right, left);
        byte[] run = Manifest(nameof(Kernels.Run));
        string runHex = Compile(bundle, run);
        if (Compile(reordered, run) != runHex)
            throw new InvalidOperationException("Reordering dependencies changed the linked field module.");
        foreach (int value in new[] { int.MinValue, -7, 0, 5, int.MaxValue })
            Record("linked fields", bundle, run, runHex, [value], () => Kernels.Run(value));
        Record("linked fields reordered", reordered, run, runHex, [5], () => Kernels.Run(5));

        byte[] read = Manifest(nameof(Kernels.ReadNull));
        byte[] write = Manifest(nameof(Kernels.WriteNull));
        Record("linked field null read", bundle, read, Compile(bundle, read), [], Kernels.ReadNull);
        Record("linked field null write", bundle, write, Compile(bundle, write), [7], () =>
        {
            Kernels.WriteNull(7);
            throw new InvalidOperationException("CLR null store did not throw.");
        });

        // Both Cell types deliberately reuse their first Field row, with different storage.
        using var leftPe = new PEReader(new MemoryStream(left));
        using var rightPe = new PEReader(new MemoryStream(right));
        MetadataReader leftMetadata = leftPe.GetMetadataReader();
        MetadataReader rightMetadata = rightPe.GetMetadataReader();
        FieldDefinitionHandle count = FirstCellField(leftMetadata);
        FieldDefinitionHandle text = FirstCellField(rightMetadata);
        if (MetadataTokens.GetRowNumber(count) != MetadataTokens.GetRowNumber(text) ||
            leftMetadata.GetString(leftMetadata.GetFieldDefinition(count).Name) != "Count" ||
            rightMetadata.GetString(rightMetadata.GetFieldDefinition(text).Name) != "Text")
            throw new InvalidOperationException("Expected a Field row collision between Count and Text.");

        // Mutate only the declaration signature from long to ulong. The Wasm
        // storage stays i64, but CLI MemberRef resolution must reject the change.
        byte[] changed = (byte[])left.Clone();
        BlobHandle signature = leftMetadata.GetFieldDefinition(count).Signature;
        int blob = leftPe.PEHeaders.MetadataStartOffset + leftMetadata.GetHeapMetadataOffset(HeapIndex.Blob) +
            MetadataTokens.GetHeapOffset(signature);
        if (!changed.AsSpan(blob, 3).SequenceEqual(new byte[] { 2, 6, 0x0a }))
            throw new InvalidOperationException("Expected the long field signature.");
        changed[blob + 2] = 0x0b;

        Reject("linked field missing owner", BundleCases.Pack(entry, right), -4);
        Reject("linked field ambiguous owner", BundleCases.Pack(entry, left, right, left), -5);
        Reject("linked field signedness mismatch", BundleCases.Pack(entry, changed, right), -6);

        void Reject(string fixture, byte[] dlls, int expected)
        {
            var method = typeof(Kernels).GetMethod(nameof(Kernels.ReadNull))!;
            int offset = IlOffsets.Find(method.GetMethodBody()!.GetILAsByteArray()!, OpCodes.Ldfld);
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

        void Record(string fixture, byte[] dlls, byte[] manifest, string hex, int[] arguments, Func<int> oracle)
        {
            int? expected = null;
            string? exception = null;
            try { expected = oracle(); }
            catch (NullReferenceException error) { exception = error.GetType().FullName; }
            cases.Add(new
            {
                module = "BootstrapLibrary", method = "CompileBundle", fixture,
                inputs = new[] { Encode(dlls), Encode(manifest) },
                generated = new { capacity, bundle = true, arguments, hex, exceptionType = exception },
                expected, exception
            });
        }

        string Compile(byte[] dlls, byte[] manifest)
        {
            var output = new byte[capacity];
            var diagnostic = CompilerExports.CreateDiagnostic();
            int length = CompilerExports.CompileBundleDetailed(dlls, manifest, output, diagnostic);
            if (length <= 0)
                throw new InvalidOperationException($"Linked field compilation returned {length} at [{string.Join(", ", diagnostic)}]");
            return Convert.ToHexString(output.AsSpan(0, length)).ToLowerInvariant();
        }
    }

    private static FieldDefinitionHandle FirstCellField(MetadataReader metadata) => metadata.TypeDefinitions
        .Select(handle => metadata.GetTypeDefinition(handle))
        .Single(type => metadata.GetString(type.Name) == "Cell")
        .GetFields().First();

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
