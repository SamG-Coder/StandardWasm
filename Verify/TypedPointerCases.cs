using System.Buffers.Binary;
using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Runtime.Loader;
using BootstrapLibrary;
using TypedPointerDemo;
using TypedPointerDependency;

internal static class TypedPointerCases
{
    internal static void Add(List<object> cases)
    {
        const int capacity = 32768;
        byte[] image = File.ReadAllBytes(typeof(Kernels).Assembly.Location);
        byte[] dependency = File.ReadAllBytes(typeof(DrawingSlot).Assembly.Location);
        byte[] bundle = BundleCases.Pack(image, dependency);
        byte[] output = new byte[capacity];
        int[] location = CompilerExports.CreateDiagnostic();

        byte[] run = Manifest(nameof(Kernels.Run));
        string runHex = Compile(nameof(Kernels.Run), run);
        foreach (int value in new[] { int.MinValue, -1, 0, 1, int.MaxValue })
            Check("round trip", run, runHex, [value], Kernels.Run(value));

        string pick = nameof(Kernels.PickPrimitive);
        byte[] pickEntries = Manifest(pick);
        Check("primitive overloads", pickEntries, Compile(pick, pickEntries), [], Kernels.PickPrimitive());

        string read = nameof(Kernels.Read);
        int load = Offset(read, OpCodes.Ldfld);
        byte[] mismatch = ReplacePointee(image, read, load);
        VerifyClrMismatch<MissingFieldException>(mismatch, dependency, read);
        Reject("distinct named pointee", BundleCases.Pack(mismatch, dependency), read, load, -6);
        string readPrimitive = nameof(Kernels.ReadPrimitive);
        int call = Offset(readPrimitive, OpCodes.Callvirt);
        byte[] primitiveMismatch = ReplacePrimitiveParameter(image, readPrimitive, call);
        VerifyClrMismatch<MissingMethodException>(primitiveMismatch, dependency, readPrimitive);
        Reject("distinct primitive pointee", BundleCases.Pack(primitiveMismatch, dependency), readPrimitive, call, -6);
        // Stage 143 (decision 0071): NativeFont* is a native address in memory 0.
        byte[] storeEntries = Manifest(nameof(Kernels.StoreFontAddress));
        Check("nonzero struct pointer", storeEntries, Compile(nameof(Kernels.StoreFontAddress), storeEntries), [],
            Kernels.StoreFontAddress());
        byte[] exportEntries = Manifest(nameof(Kernels.ExportNullFont));
        Check("null struct pointer export", exportEntries, Compile(nameof(Kernels.ExportNullFont), exportEntries), [], 0);
        // Stage 132 (decision 0060): reading through the null int* field raises the
        // typed NullReferenceException on both runtimes. The module imports memory 0.
        string dereference = nameof(Kernels.DereferenceNullField);
        try
        {
            Kernels.DereferenceNullField();
            throw new InvalidOperationException("The CLR read through a null int* field.");
        }
        catch (NullReferenceException)
        {
            byte[] dereferenceEntries = Manifest(dereference);
            cases.Add(new
            {
                module = "BootstrapLibrary", method = "CompileBundle", fixture = "typed pointer null field dereference",
                inputs = new[] { Encode(bundle), Encode(dereferenceEntries) },
                generated = new
                {
                    capacity, bundle = true, arguments = Array.Empty<int>(), hex = Compile(dereference, dereferenceEntries),
                    memory = true, exceptionType = typeof(NullReferenceException).FullName
                },
                expected = (int?)null, exception = typeof(NullReferenceException).FullName
            });
        }

        string Compile(string name, byte[] entries)
        {
            int size = CompilerExports.CompileBundleDetailed(bundle, entries, output, location);
            if (size <= 0)
                throw new InvalidOperationException($"{name} returned {size} at [{string.Join(", ", location)}].");
            return Convert.ToHexString(output.AsSpan(0, size)).ToLowerInvariant();
        }

        void Check(string label, byte[] entries, string hex, int[] arguments, int expected)
        {
            cases.Add(new
            {
                module = "BootstrapLibrary", method = "CompileBundle", fixture = "typed pointer " + label,
                inputs = new[] { Encode(bundle), Encode(entries) },
                generated = new { capacity, bundle = true, arguments, hex },
                expected
            });
        }

        void Reject(string label, byte[] input, string method, int il, int expected)
        {
            byte[] entries = Manifest(method);
            int[] diagnostic = [0, Row(method), il];
            Array.Fill(output, (byte)0xa5);
            int status = CompilerExports.CompileBundleDetailed(input, entries, output, location);
            if (status != expected || !location.SequenceEqual(diagnostic) || output.Any(value => value != 0xa5))
                throw new InvalidOperationException($"{label} returned {status} at [{string.Join(", ", location)}].");
            cases.Add(new
            {
                module = "BootstrapLibrary", method = "CompileBundle", fixture = "typed pointer " + label,
                inputs = new[] { Encode(input), Encode(entries) },
                generated = new { capacity, bundle = true, arguments = Array.Empty<int>(), reject = true, diagnostic },
                expected = status
            });
        }
    }

    private static byte[] ReplacePointee(byte[] image, string method, int load)
    {
        using var pe = new PEReader(new MemoryStream(image));
        var metadata = pe.GetMetadataReader();
        int token = BinaryPrimitives.ReadInt32LittleEndian(Il(method).AsSpan(load + 1));
        var member = metadata.GetMemberReference((MemberReferenceHandle)MetadataTokens.Handle(token));
        var signature = metadata.GetBlobReader(member.Signature);
        if (signature.ReadByte() != 6 || signature.ReadByte() != 0x0f || signature.ReadByte() != 0x11 ||
            metadata.GetString(metadata.GetTypeReference((TypeReferenceHandle)signature.ReadTypeHandle()).Name) != nameof(NativeFont) ||
            signature.RemainingBytes != 0)
            throw new InvalidOperationException("Missing linked NativeFont pointer signature.");

        var other = metadata.TypeReferences.Single(handle => metadata.GetString(metadata.GetTypeReference(handle).Name) == nameof(NativePen));
        var replacement = new BlobBuilder();
        replacement.WriteCompressedInteger(MetadataTokens.GetRowNumber(other) << 2 | 1);
        int offset = pe.PEHeaders.MetadataStartOffset + metadata.GetHeapMetadataOffset(HeapIndex.Blob) +
            MetadataTokens.GetHeapOffset(member.Signature);
        if (image[offset] != 3 + replacement.Count)
            throw new InvalidOperationException("The fixture needs equal-width TypeRef encodings.");
        byte[] mismatch = (byte[])image.Clone();
        replacement.ToArray().CopyTo(mismatch.AsSpan(offset + 4));
        return mismatch;
    }

    private static byte[] ReplacePrimitiveParameter(byte[] image, string method, int call)
    {
        using var pe = new PEReader(new MemoryStream(image));
        var metadata = pe.GetMetadataReader();
        int token = BinaryPrimitives.ReadInt32LittleEndian(Il(method).AsSpan(call + 1));
        var member = metadata.GetMemberReference((MemberReferenceHandle)MetadataTokens.Handle(token));
        // Instance method with one parameter: int32 ReadSigned(int32*).
        byte[] signature = [0x20, 1, 8, 0x0f, 8];
        if (!metadata.GetBlobBytes(member.Signature).AsSpan().SequenceEqual(signature))
            throw new InvalidOperationException("Missing linked int32 pointer parameter.");
        int offset = pe.PEHeaders.MetadataStartOffset + metadata.GetHeapMetadataOffset(HeapIndex.Blob) +
            MetadataTokens.GetHeapOffset(member.Signature);
        if (image[offset] != signature.Length)
            throw new InvalidOperationException("The fixture needs a one-byte blob size.");
        byte[] mismatch = (byte[])image.Clone();
        mismatch[offset + signature.Length] = 9; // ELEMENT_TYPE_U4
        return mismatch;
    }

    private static void VerifyClrMismatch<TException>(byte[] image, byte[] dependency, string name)
        where TException : Exception
    {
        var context = new AssemblyLoadContext("Typed pointer oracle", isCollectible: true);
        try
        {
            var slot = context.LoadFromStream(new MemoryStream(dependency)).GetType(typeof(DrawingSlot).FullName!)!;
            var method = context.LoadFromStream(new MemoryStream(image)).GetType(typeof(Kernels).FullName!)!
                .GetMethod(name)!;
            method.Invoke(null, [Activator.CreateInstance(slot)]);
            throw new InvalidOperationException($"The CLR accepted a different pointer pointee in {name}.");
        }
        catch (TargetInvocationException error) when (error.InnerException is TException) { }
        finally { context.Unload(); }
    }

    private static byte[] Il(string name) => typeof(Kernels).GetMethod(name)!.GetMethodBody()!.GetILAsByteArray()!;
    private static int Row(string name) => typeof(Kernels).GetMethod(name)!.MetadataToken & 0xffffff;
    private static int Offset(string method, OpCode opcode)
    {
        int offset = IlOffsets.Find(Il(method), opcode);
        return offset >= 0 ? offset : throw new InvalidOperationException($"{method} did not emit {opcode}.");
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
