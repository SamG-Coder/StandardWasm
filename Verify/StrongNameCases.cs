using System.Buffers.Binary;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using BootstrapLibrary;
using SignedDependency;
using StrongNameDemo;

internal static class StrongNameCases
{
    internal static void Add(List<object> cases)
    {
        const int capacity = 16384;
        string rightPath = typeof(Operations).Assembly.Location;
        string wrongPath = Path.Combine(AppContext.BaseDirectory, "StrongNameAlternate.dll");
        var rightName = AssemblyName.GetAssemblyName(rightPath);
        var wrongName = AssemblyName.GetAssemblyName(wrongPath);
        byte[] rightToken = rightName.GetPublicKeyToken()!, wrongToken = wrongName.GetPublicKeyToken()!;
        if (rightName.Name != wrongName.Name || rightName.Version != wrongName.Version ||
            rightToken.Length != 8 || wrongToken.Length != 8 || rightToken.SequenceEqual(wrongToken))
            throw new InvalidOperationException("Expected same-name, same-version assemblies signed with different keys.");
        byte[] main = File.ReadAllBytes(typeof(Entry).Assembly.Location);
        byte[] right = File.ReadAllBytes(rightPath);
        byte[] wrong = File.ReadAllBytes(wrongPath);
        var method = typeof(Entry).GetMethod(nameof(Entry.Run))!;
        byte[] manifest = new byte[11];
        BinaryPrimitives.WriteInt32LittleEndian(manifest, method.MetadataToken & 0xffffff);
        manifest[4] = 3;
        "run"u8.CopyTo(manifest.AsSpan(8));
        object encodedManifest = Encode(manifest);
        byte[] output = new byte[capacity];
        string hex = Check("public-key token", BundleCases.Pack(main, right));
        if (Check("same-name alternatives", BundleCases.Pack(main, wrong, right)) != hex ||
            Check("reordered same-name alternatives", BundleCases.Pack(main, right, wrong)) != hex)
            throw new InvalidOperationException("The unrelated key changed the emitted module.");

        byte[] fullKey = UseFullKey(main, rightName, rightToken);
        if (Check("full public key", BundleCases.Pack(fullKey, right)) != hex)
            throw new InvalidOperationException("Full-key binding and token binding emitted different modules.");
        Reject("different public-key token", BundleCases.Pack(main, wrong), -4);
        Reject("different full public key", BundleCases.Pack(fullKey, wrong), -4);
        Reject("duplicate matching identity", BundleCases.Pack(main, right, right), -5);

        string Check(string name, byte[] bundle)
        {
            int length = CompilerExports.CompileBundle(bundle, manifest, output);
            if (length <= 0) throw new InvalidOperationException($"{name} returned {length}");
            string wasm = Convert.ToHexString(output.AsSpan(0, length)).ToLowerInvariant();
            object[] inputs = [Encode(bundle), encodedManifest];
            foreach (int value in new[] { int.MinValue, 0, 5, int.MaxValue })
                cases.Add(new
                {
                    module = "BootstrapLibrary", method = "CompileBundle", fixture = name,
                    inputs, generated = new { capacity, bundle = true, arguments = new[] { value }, hex = wasm },
                    expected = Entry.Run(value)
                });
            return wasm;
        }

        void Reject(string name, byte[] bundle, int expected)
        {
            Array.Fill(output, (byte)0xa5);
            int status = CompilerExports.CompileBundle(bundle, manifest, output);
            if (status != expected || output.Any(value => value != 0xa5))
                throw new InvalidOperationException($"{name} returned {status}");
            cases.Add(new
            {
                module = "BootstrapLibrary", method = "CompileBundle", fixture = name,
                inputs = new[] { Encode(bundle), encodedManifest },
                generated = new { capacity, bundle = true, arguments = Array.Empty<int>(), reject = true },
                expected
            });
        }
    }

    private static byte[] UseFullKey(byte[] main, AssemblyName right, byte[] token)
    {
        using var pe = new PEReader(new MemoryStream(main));
        var md = pe.GetMetadataReader();
        var reference = md.AssemblyReferences.Single(h =>
            md.GetString(md.GetAssemblyReference(h).Name) == right.Name);
        var def = md.GetAssemblyDefinition();
        if (!md.GetBlobBytes(def.PublicKey).SequenceEqual(right.GetPublicKey()!) ||
            !md.GetBlobBytes(md.GetAssemblyReference(reference).PublicKeyOrToken).SequenceEqual(token))
            throw new InvalidOperationException("The main assembly and dependency should use the same test key.");
        int width = md.GetHeapSize(HeapIndex.Blob) < 65536 ? 2 : 4;
        int row = pe.PEHeaders.MetadataStartOffset + md.GetTableMetadataOffset(TableIndex.AssemblyRef) +
            (MetadataTokens.GetRowNumber(reference) - 1) * md.GetTableRowSize(TableIndex.AssemblyRef);
        byte[] changed = (byte[])main.Clone();
        BinaryPrimitives.WriteUInt32LittleEndian(changed.AsSpan(row + 8), (uint)AssemblyFlags.PublicKey);
        int key = MetadataTokens.GetHeapOffset(def.PublicKey);
        if (width == 2) BinaryPrimitives.WriteUInt16LittleEndian(changed.AsSpan(row + 12), checked((ushort)key));
        else BinaryPrimitives.WriteInt32LittleEndian(changed.AsSpan(row + 12), key);
        return changed;
    }

    private static object Encode(byte[] bytes) => new { bytes = Array.ConvertAll(bytes, value => (int)value) };
}
