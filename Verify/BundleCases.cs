using System.Buffers.Binary;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using BundleLibrary;

internal static class BundleCases
{
    internal static void Add(List<object> cases)
    {
        const int capacity = 32768;
        byte[] entry = File.ReadAllBytes(typeof(Kernels).Assembly.Location);
        byte[] library = File.ReadAllBytes(typeof(SharedMath.MathLibrary).Assembly.Location);
        byte[] leaf = File.ReadAllBytes(typeof(LeafMath.Leaf).Assembly.Location);
        byte[] manifest = new byte[11];
        BinaryPrimitives.WriteInt32LittleEndian(manifest,
            typeof(Kernels).GetMethod(nameof(Kernels.Calculate))!.MetadataToken & 0xffffff);
        manifest[4] = 3;
        "run"u8.CopyTo(manifest.AsSpan(8));

        byte[] joined = Pack(entry, library, leaf);
        byte[] reordered = Pack(entry, leaf, library);
        var output = new byte[capacity];
        int size = BundleDemo.CompileBundle(joined, manifest, output);
        if (size <= 0) throw new InvalidOperationException($"Bundle linker returned {size}.");
        string hex = Convert.ToHexString(output.AsSpan(0, size)).ToLowerInvariant();
        int reversed = BundleDemo.CompileBundle(reordered, manifest, output);
        if (reversed != size || hex != Convert.ToHexString(output.AsSpan(0, size)).ToLowerInvariant())
            throw new InvalidOperationException("Explicit dependency ordering changed the linked module.");

        var inputs = new[] { Encode(joined), Encode(manifest) };
        foreach (int value in new[] { int.MinValue, -101, -1, 0, 1, 9, 100, int.MaxValue })
            AddCase("overloaded and transitive call", inputs, new[] { value }, Kernels.Calculate(value), false);
        AddCase("reordered dependencies", new[] { Encode(reordered), Encode(manifest) }, [9], 89, false);

        Reject("missing direct dependency", Pack(entry, leaf), -4);
        Reject("missing transitive dependency", Pack(entry, library), -4);
        Reject("ambiguous AssemblyDef identity", Pack(entry, library, leaf, library), -5);
        Reject("truncated bundle", joined[..^1], -1);

        using var pe = new PEReader(new MemoryStream(library));
        var metadata = pe.GetMetadataReader();
        int metadataStart = pe.PEHeaders.MetadataStartOffset;
        int version = metadataStart + metadata.GetTableMetadataOffset(TableIndex.Assembly) + 4;
        byte[] wrongVersion = (byte[])library.Clone();
        BinaryPrimitives.WriteUInt16LittleEndian(wrongVersion.AsSpan(version), 999);
        Reject("dependency version mismatch", Pack(entry, wrongVersion, leaf), -4);

        int row = typeof(SharedMath.MathLibrary).GetMethod(nameof(SharedMath.MathLibrary.Blend))!.MetadataToken & 0xffffff;
        var definition = metadata.GetMethodDefinition(MetadataTokens.MethodDefinitionHandle(row));
        int methodName = metadataStart + metadata.GetHeapMetadataOffset(HeapIndex.String) +
            MetadataTokens.GetHeapOffset(definition.Name);
        byte[] missingMethod = (byte[])library.Clone();
        missingMethod[methodName] = (byte)'Z';
        Reject("missing matching method", Pack(entry, missingMethod, leaf), -6);

        void Reject(string name, byte[] bundle, int expected)
        {
            Array.Fill(output, (byte)0xa5);
            int status = BundleDemo.CompileBundle(bundle, manifest, output);
            if (status != expected || output.Any(value => value != 0xa5))
                throw new InvalidOperationException($"{name} returned {status}, expected {expected}.");
            AddCase(name, new[] { Encode(bundle), Encode(manifest) }, [], expected, true);
        }

        void AddCase(string name, object[] args, int[] arguments, int expected, bool reject)
        {
            cases.Add(new
            {
                module = "BundleLibrary", method = "CompileBundle", fixture = name, inputs = args,
                generated = new { capacity, arguments, bundle = true, hex = reject ? null : hex, reject }, expected
            });
        }
    }

    private static byte[] Pack(params byte[][] assemblies)
    {
        using var buffer = new MemoryStream();
        using var writer = new BinaryWriter(buffer);
        foreach (byte[] assembly in assemblies)
        {
            writer.Write(assembly.Length);
            writer.Write(assembly);
        }
        return buffer.ToArray();
    }

    private static object Encode(byte[] bytes) => new { bytes = Array.ConvertAll(bytes, value => (int)value) };
}
