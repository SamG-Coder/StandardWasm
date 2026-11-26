using System.Buffers.Binary;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using BootstrapLibrary;
using PointerFieldDemo;
using PointerFieldDependency;

internal static class PointerFieldCases
{
    internal static void Add(List<object> cases)
    {
        const int capacity = 32768;
        byte[] image = File.ReadAllBytes(typeof(Kernels).Assembly.Location);
        byte[] dependency = File.ReadAllBytes(typeof(NativeSlot).Assembly.Location);
        byte[] bundle = BundleCases.Pack(image, dependency);
        byte[] output = new byte[capacity];
        int[] location = CompilerExports.CreateDiagnostic();

        var (runInputs, runHex) = Compile(nameof(Kernels.Run));
        foreach (int value in new[] { int.MinValue, -1, 0, 1, int.MaxValue })
            cases.Add(new
            {
                module = "BootstrapLibrary", method = "CompileBundle", fixture = "pointer field round trip", inputs = runInputs,
                generated = new { capacity, bundle = true, arguments = new[] { value }, hex = runHex }, expected = Kernels.Run(value)
            });

        var (emptyInputs, emptyHex) = Compile(nameof(Kernels.ReadEmpty));
        cases.Add(new
        {
            module = "BootstrapLibrary", method = "CompileBundle", fixture = "pointer field managed return",
            inputs = emptyInputs, generated = new { capacity, bundle = true, arguments = Array.Empty<int>(), hex = emptyHex },
            expected = Kernels.ReadEmpty()
        });

        string read = nameof(Kernels.ReadNullSlot);
        var (nullInputs, nullHex) = Compile(read);
        try
        {
            Kernels.ReadNullSlot(null);
            throw new InvalidOperationException("The CLR did not throw on a null field receiver.");
        }
        catch (NullReferenceException error)
        {
            string exception = error.GetType().FullName!;
            cases.Add(new
            {
                module = "BootstrapLibrary", method = "CompileBundle", fixture = "pointer field null receiver", inputs = nullInputs,
                generated = new { capacity, bundle = true, arguments = new object[] { null }, hex = nullHex, exceptionType = exception },
                expected = (int?)null, exception
            });
        }

        // Stage 137 (decision 0060): void* is a native address in memory 0.
        foreach (int value in new[] { int.MinValue, -1, 0, 7, int.MaxValue })
            Positive("nonzero void* address", nameof(Kernels.StoreAddress), [value], Kernels.StoreAddress(value), null);
        // The CLR returns a null void*, which is address 0 (ReadEmpty checks the same null
        // return inside managed code).
        Positive("null void* export", nameof(Kernels.ExportNull), [], 0, null);
        try
        {
            Kernels.DereferenceNullAddress();
            throw new InvalidOperationException("The CLR read through a null void* field.");
        }
        catch (NullReferenceException error)
        {
            Positive("null void* dereference", nameof(Kernels.DereferenceNullAddress), [], null, error.GetType().FullName,
                memory: true);
        }
        Reject("function pointer field", bundle, nameof(Kernels.RejectCallbackLayout), OpCodes.Newobj, -2);

        using var pe = new PEReader(new MemoryStream(image));
        var metadata = pe.GetMetadataReader();
        int token = BinaryPrimitives.ReadInt32LittleEndian(MethodIl(read).AsSpan(Offset(read, OpCodes.Ldfld) + 1));
        var member = metadata.GetMemberReference((MemberReferenceHandle)MetadataTokens.Handle(token));
        int blob = pe.PEHeaders.MetadataStartOffset + metadata.GetHeapMetadataOffset(HeapIndex.Blob) +
            MetadataTokens.GetHeapOffset(member.Signature);
        if (!image.AsSpan(blob, 4).SequenceEqual(new byte[] { 3, 6, 0x0f, 1 }))
            throw new InvalidOperationException("Missing void-pointer field signature.");
        byte[] native = (byte[])image.Clone();
        native[blob] = 2;
        native[blob + 2] = 0x19; // Native unsigned integer is a different signature.
        Reject("native integer signature", BundleCases.Pack(native, dependency), read, OpCodes.Ldfld, -6);
        byte[] byref = (byte[])image.Clone();
        byref[blob + 2] = 0x10;
        byref[blob + 3] = 8; // A managed byref to Int32.
        Reject("managed byref signature", BundleCases.Pack(byref, dependency), read, OpCodes.Ldfld, -6);

        (object[] Inputs, string Hex) Compile(string name)
        {
            byte[] entries = Manifest(name);
            int size = CompilerExports.CompileBundleDetailed(bundle, entries, output, location);
            if (size <= 0)
                throw new InvalidOperationException($"{name} returned {size} at [{string.Join(", ", location)}].");
            return ([Encode(bundle), Encode(entries)], Convert.ToHexString(output.AsSpan(0, size)).ToLowerInvariant());
        }

        void Positive(string label, string name, object[] arguments, int? expected, string? exception, bool memory = false)
        {
            var (inputs, hex) = Compile(name);
            cases.Add(new
            {
                module = "BootstrapLibrary", method = "CompileBundle", fixture = "pointer field " + label, inputs,
                generated = memory
                    ? (object)new { capacity, bundle = true, arguments, hex, exceptionType = exception, memory = true }
                    : new { capacity, bundle = true, arguments, hex, exceptionType = exception },
                expected, exception
            });
        }

        void Reject(string label, byte[] input, string name, OpCode? opcode, int expected)
        {
            byte[] entries = Manifest(name);
            int[] diagnostic = [0, Row(name), opcode.HasValue ? Offset(name, opcode.Value) : -1];
            Array.Fill(output, (byte)0xa5);
            int status = CompilerExports.CompileBundleDetailed(input, entries, output, location);
            if (status != expected || !location.SequenceEqual(diagnostic) || output.Any(b => b != 0xa5))
                throw new InvalidOperationException($"{label} returned {status} at [{string.Join(", ", location)}].");
            cases.Add(new
            {
                module = "BootstrapLibrary", method = "CompileBundle", fixture = "pointer field " + label,
                inputs = new[] { Encode(input), Encode(entries) },
                generated = new { capacity, bundle = true, arguments = Array.Empty<int>(), reject = true, diagnostic },
                expected = status
            });
        }
    }

    private static byte[] MethodIl(string name) => typeof(Kernels).GetMethod(name)!.GetMethodBody()!.GetILAsByteArray()!;
    private static int Row(string name) => typeof(Kernels).GetMethod(name)!.MetadataToken & 0xffffff;

    private static int Offset(string method, OpCode opcode)
    {
        int offset = IlOffsets.Find(MethodIl(method), opcode);
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
