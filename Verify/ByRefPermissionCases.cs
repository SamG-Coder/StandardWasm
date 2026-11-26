using System.Buffers.Binary;
using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Runtime.Loader;
using BootstrapLibrary;
using ByRefPermissionFixture;

internal static class ByRefPermissionCases
{
    internal static void Add(List<object> cases)
    {
        const int capacity = 32768;
        byte[] consumer = File.ReadAllBytes(typeof(Kernels).Assembly.Location);
        byte[] provider = File.ReadAllBytes(typeof(Frame).Assembly.Location);
        byte[] bundle = BundleCases.Pack(consumer, provider);
        byte[] output = new byte[capacity];
        int[] location = CompilerExports.CreateDiagnostic();

        if ((int)typeof(IMatch<>).GetGenericArguments()[0].GenericParameterAttributes != 0x22 ||
            !typeof(Borrowed<int>).IsByRefLike || typeof(Element<int>).IsByRefLike)
            throw new InvalidOperationException("The CLR fixture did not declare the expected permission and types.");

        string run = nameof(Kernels.Run);
        byte[] entries = Manifest(run);
        int size = CompilerExports.CompileBundleDetailed(bundle, entries, output, location);
        if (size < 1) throw new InvalidOperationException($"{run} returned {size} at [{string.Join(", ", location)}].");
        string hex = Convert.ToHexString(output.AsSpan(0, size)).ToLowerInvariant();
        foreach (int value in new[] { int.MinValue, -23, 0, 23, int.MaxValue })
            cases.Add(new
            {
                module = "BootstrapLibrary", method = "CompileBundle", fixture = "byref permission ordinary arguments",
                inputs = new[] { Encode(bundle), Encode(entries) },
                generated = new { capacity, bundle = true, arguments = new[] { value }, hex },
                expected = Kernels.Run(value)
            });

        if (Kernels.RejectBorrowed() != 1)
            throw new InvalidOperationException("The CLR did not execute the declared borrowed-type fixture.");
        string borrowed = nameof(Kernels.RejectBorrowed);
        Reject(bundle, borrowed, Offset(borrowed, OpCodes.Newobj), -2, "actual byref-like argument");

        string read = nameof(Kernels.ReadScalar);
        int load = Offset(read, OpCodes.Ldfld);
        int token = BinaryPrimitives.ReadInt32LittleEndian(Il(read).AsSpan(load + 1));
        byte[] mismatch = ReplaceScalarArgument(consumer, token);
        AssertClrMismatch(mismatch, provider);
        Reject(BundleCases.Pack(mismatch, provider), read, load, -6, "distinct scalar identity");

        void Reject(byte[] bytes, string method, int il, int expected, string label)
        {
            byte[] manifest = Manifest(method);
            int[] diagnostic = [0, Row(method), il];
            Array.Fill(output, (byte)0xa5);
            int status = CompilerExports.CompileBundleDetailed(bytes, manifest, output, location);
            if (status != expected || !location.SequenceEqual(diagnostic) || output.Any(value => value != 0xa5))
                throw new InvalidOperationException($"{label} returned {status} at [{string.Join(", ", location)}].");
            cases.Add(new
            {
                module = "BootstrapLibrary", method = "CompileBundle", fixture = "byref permission " + label,
                inputs = new[] { Encode(bytes), Encode(manifest) },
                generated = new { capacity, bundle = true, arguments = Array.Empty<int>(), reject = true, diagnostic },
                expected
            });
        }
    }

    private static byte[] ReplaceScalarArgument(byte[] image, int token)
    {
        using var pe = new PEReader(new MemoryStream(image));
        var metadata = pe.GetMetadataReader();
        var field = metadata.GetMemberReference((MemberReferenceHandle)MetadataTokens.Handle(token));
        var signature = metadata.GetBlobReader(field.Signature);
        if (signature.ReadByte() != 6 || signature.ReadByte() != 0x15 || signature.ReadByte() != 0x12 ||
            signature.ReadTypeHandle().Kind != HandleKind.TypeReference || signature.ReadCompressedInteger() != 1)
            throw new InvalidOperationException("The fixture must read a linked closed interface field.");
        int element = signature.Offset;
        if (signature.ReadByte() != 8 || signature.RemainingBytes != 0)
            throw new InvalidOperationException("The fixture must use the CLI Int32 encoding.");
        int offset = pe.PEHeaders.MetadataStartOffset + metadata.GetHeapMetadataOffset(HeapIndex.Blob) +
            MetadataTokens.GetHeapOffset(field.Signature);
        if (image[offset] != metadata.GetBlobBytes(field.Signature).Length)
            throw new InvalidOperationException("Expected a single-byte signature size.");
        byte[] mismatch = (byte[])image.Clone();
        mismatch[offset + 1 + element] = 9;
        return mismatch;
    }

    private static void AssertClrMismatch(byte[] consumer, byte[] provider)
    {
        var context = new AssemblyLoadContext("Byref permission identity oracle", isCollectible: true);
        try
        {
            var frame = context.LoadFromStream(new MemoryStream(provider)).GetType(typeof(Frame).FullName!)!;
            var entry = context.LoadFromStream(new MemoryStream(consumer)).GetType(typeof(Kernels).FullName!)!
                .GetMethod(nameof(Kernels.ReadScalar))!;
            entry.Invoke(null, [Activator.CreateInstance(frame)]);
            throw new InvalidOperationException("The CLR accepted a different closed interface in a field signature.");
        }
        catch (TargetInvocationException error) when (error.InnerException is MissingFieldException) { }
        finally { context.Unload(); }
    }

    private static byte[] Il(string name) => typeof(Kernels).GetMethod(name)!.GetMethodBody()!.GetILAsByteArray()!;
    private static int Row(string name) => typeof(Kernels).GetMethod(name)!.MetadataToken & 0xffffff;
    private static int Offset(string name, OpCode opcode)
    {
        int offset = IlOffsets.Find(Il(name), opcode);
        return offset >= 0 ? offset : throw new InvalidOperationException($"{name} did not emit {opcode}.");
    }

    private static byte[] Manifest(string name)
    {
        byte[] entries = new byte[11];
        BinaryPrimitives.WriteInt32LittleEndian(entries, Row(name));
        entries[4] = 3;
        "run"u8.CopyTo(entries.AsSpan(8));
        return entries;
    }

    private static object Encode(byte[] bytes) => new { bytes = Array.ConvertAll(bytes, value => (int)value) };
}
