extern alias ThirdParty;

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
        byte[] manifest = Manifest(typeof(Kernels), nameof(Kernels.Calculate));

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
            AddCase("overloaded and transitive call", inputs, new[] { value }, Kernels.Calculate(value), hex);
        AddCase("reordered dependencies", new[] { Encode(reordered), Encode(manifest) }, [9], 89, hex);

        Reject("missing direct dependency", Pack(entry, leaf), -4);
        Reject("missing transitive dependency", Pack(entry, library), -4);
        Reject("ambiguous AssemblyDef identity", Pack(entry, library, leaf, library), -5);
        Reject("truncated bundle", joined[..^1], -1);
        Reject("zero-length final image", [..joined, 0, 0, 0, 0], -1);

        using var pe = new PEReader(new MemoryStream(library));
        var metadata = pe.GetMetadataReader();
        int metadataStart = pe.PEHeaders.MetadataStartOffset;
        int version = metadataStart + metadata.GetTableMetadataOffset(TableIndex.Assembly) + 4;
        byte[] wrongVersion = (byte[])library.Clone();
        BinaryPrimitives.WriteUInt16LittleEndian(wrongVersion.AsSpan(version), 999);
        Reject("dependency version mismatch", Pack(entry, wrongVersion, leaf), -4);

        // Reusing the caller's array in a later invocation must take a new
        // snapshot. The changed image has identical lengths and owner indices.
        byte[] reused = (byte[])joined.Clone();
        int libraryStart = 8 + entry.Length;
        int first = BundleDemo.CompileBundle(reused, manifest, output);
        if (first != size || Convert.ToHexString(output.AsSpan(0, first)).ToLowerInvariant() != hex)
            throw new InvalidOperationException("Initial compile of reusable bundle changed the output.");
        wrongVersion.CopyTo(reused, libraryStart);
        Reject("changed dependency in reused bundle", reused, -4);
        library.CopyTo(reused, libraryStart);
        int restored = BundleDemo.CompileBundle(reused, manifest, output);
        if (restored != size || Convert.ToHexString(output.AsSpan(0, restored)).ToLowerInvariant() != hex)
            throw new InvalidOperationException("Restored bundle did not reproduce the module.");
        AddCase("restored dependency in reused bundle", new[] { Encode(reused), Encode(manifest) }, [9], 89, hex);

        int row = typeof(SharedMath.MathLibrary).GetMethod(nameof(SharedMath.MathLibrary.Blend))!.MetadataToken & 0xffffff;
        var definition = metadata.GetMethodDefinition(MetadataTokens.MethodDefinitionHandle(row));
        int methodName = metadataStart + metadata.GetHeapMetadataOffset(HeapIndex.String) +
            MetadataTokens.GetHeapOffset(definition.Name);
        byte[] missingMethod = (byte[])library.Clone();
        missingMethod[methodName] = (byte)'Z';
        Reject("missing matching method", Pack(entry, missingMethod, leaf), -6);

        var aliasedType = typeof(TextIdentityProbe.Kernels);
        byte[] text = File.ReadAllBytes(aliasedType.Assembly.Location);
        byte[] dependency = File.ReadAllBytes(typeof(ThirdParty::System.String).Assembly.Location);
        byte[] aliased = Pack(text, dependency);
        byte[] scalar = Manifest(aliasedType, nameof(TextIdentityProbe.Kernels.ConcatScalar));
        int linked = BundleDemo.CompileBundle(aliased, scalar, output);
        if (linked <= 0) throw new InvalidOperationException($"Aliased scalar overload returned {linked}.");
        string linkedHex = Convert.ToHexString(output.AsSpan(0, linked)).ToLowerInvariant();
        var aliasedInputs = new[] { Encode(aliased), Encode(scalar) };
        foreach (int value in new[] { int.MinValue, 9, int.MaxValue })
            AddCase("aliased String scalar overload", aliasedInputs, [value],
                TextIdentityProbe.Kernels.ConcatScalar(value), linkedHex);

        byte[] concat = Manifest(aliasedType, nameof(TextIdentityProbe.Kernels.ConcatLength));
        Reject("aliased String with missing dependency", Pack(text), -4, concat);
        // Main rejected this call (-2) because linked calls could not return a string. The stack's
        // linked references (#125) lower platform strings across DLLs, so the third-party
        // Concat(string, string) now compiles. Its result must still match the CLR, which runs the
        // third-party body, not the BCL Concat.
        int concatLinked = BundleDemo.CompileBundle(aliased, concat, output);
        if (concatLinked <= 0) throw new InvalidOperationException($"Aliased String Concat returned {concatLinked}.");
        AddCase("aliased String with string return type", new[] { Encode(aliased), Encode(concat) }, [],
            TextIdentityProbe.Kernels.ConcatLength(),
            Convert.ToHexString(output.AsSpan(0, concatLinked)).ToLowerInvariant());
        // Likewise, the stack's linked objects (#127, #131) construct a third-party class and call its
        // instance members. The CLR returns the third-party Length (99), not a BCL string length.
        byte[] construct = Manifest(aliasedType, nameof(TextIdentityProbe.Kernels.Construct));
        int constructLinked = BundleDemo.CompileBundle(aliased, construct, output);
        if (constructLinked <= 0) throw new InvalidOperationException($"Aliased String constructor returned {constructLinked}.");
        AddCase("aliased String constructor", new[] { Encode(aliased), Encode(construct) }, [],
            TextIdentityProbe.Kernels.Construct(),
            Convert.ToHexString(output.AsSpan(0, constructLinked)).ToLowerInvariant());

        void Reject(string name, byte[] bundle, int expected, byte[] selection = null)
        {
            selection ??= manifest;
            Array.Fill(output, (byte)0xa5);
            int status = BundleDemo.CompileBundle(bundle, selection, output);
            if (status != expected || output.Any(value => value != 0xa5))
                throw new InvalidOperationException($"{name} returned {status}, expected {expected}.");
            AddCase(name, new[] { Encode(bundle), Encode(selection) }, [], expected, null);
        }

        void AddCase(string name, object[] args, int[] arguments, int expected, string moduleHex)
        {
            cases.Add(new
            {
                module = "BundleLibrary", method = "CompileBundle", fixture = name, inputs = args,
                generated = new { capacity, arguments, bundle = true, hex = moduleHex, reject = moduleHex is null }, expected
            });
        }
    }

    internal static byte[] Manifest(Type type, string name)
    {
        var manifest = new byte[11];
        BinaryPrimitives.WriteInt32LittleEndian(manifest, type.GetMethod(name)!.MetadataToken & 0xffffff);
        manifest[4] = 3;
        "run"u8.CopyTo(manifest.AsSpan(8));
        return manifest;
    }

    internal static byte[] Pack(params byte[][] assemblies)
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
