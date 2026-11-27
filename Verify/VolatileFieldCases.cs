using System.Buffers.Binary;
using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Runtime.Loader;
using BootstrapLibrary;
using VolatileFieldDemo;
using VolatileFieldDependency;

internal static class VolatileFieldCases
{
    internal static void Add(List<object> cases)
    {
        const int capacity = 32768;
        byte[] image = File.ReadAllBytes(typeof(Kernels).Assembly.Location);
        byte[] dependency = File.ReadAllBytes(typeof(Mailbox).Assembly.Location);
        byte[] bundle = BundleCases.Pack(image, dependency);
        byte[] output = new byte[capacity];
        int[] location = CompilerExports.CreateDiagnostic();

        string run = nameof(Kernels.Run);
        string read = nameof(Kernels.Read);
        byte[] runIl = MethodIl(run);
        if (IlOffsets.Find(runIl, OpCodes.Volatile) < 0)
            throw new InvalidOperationException("The fixture did not emit a volatile field prefix.");
        var (runInputs, runHex) = Compile(bundle, run);
        foreach (int value in new[] { int.MinValue, -32769, -1, 0, 1, 32768, int.MaxValue })
            cases.Add(new
            {
                module = "BootstrapLibrary", method = "CompileBundle", fixture = "volatile field published fields",
                inputs = runInputs, generated = new { capacity, bundle = true, arguments = new[] { value }, hex = runHex },
                expected = Kernels.Run(value)
            });

        using var pe = new PEReader(new MemoryStream(image));
        var metadata = pe.GetMetadataReader();
        byte[] readIl = MethodIl(read);
        int prefix = Offset(read);
        int field = BinaryPrimitives.ReadInt32LittleEndian(readIl.AsSpan(prefix + 3));
        var member = metadata.GetMemberReference((MemberReferenceHandle)MetadataTokens.Handle(field));
        int heap = pe.PEHeaders.MetadataStartOffset + metadata.GetHeapMetadataOffset(HeapIndex.Blob);
        int signature = heap + MetadataTokens.GetHeapOffset(member.Signature);
        var blob = metadata.GetBlobReader(member.Signature);
        if (blob.ReadByte() != 6 || blob.ReadByte() != 0x1f)
            throw new InvalidOperationException("The linked field is missing modreq.");
        var marker = metadata.GetTypeReference((TypeReferenceHandle)blob.ReadTypeHandle());
        if (metadata.GetString(marker.Name) != "IsVolatile" || blob.ReadByte() != 8 || blob.RemainingBytes != 0)
            throw new InvalidOperationException("The fixture did not encode an IsVolatile Int32.");

        CheckNull("prefixed read", bundle, () => Kernels.Read(null));
        byte[] plainInstruction = (byte[])image.Clone();
        int rva = metadata.GetMethodDefinition(MetadataTokens.MethodDefinitionHandle(Row(read))).RelativeVirtualAddress;
        var section = pe.PEHeaders.SectionHeaders[pe.PEHeaders.GetContainingSectionIndex(rva)];
        int body = section.PointerToRawData + rva - section.VirtualAddress;
        int header = (image[body] & 3) == 2 ? 1 : (image[body + 1] >> 4) * 4;
        plainInstruction[body + header + prefix] = 0;
        plainInstruction[body + header + prefix + 1] = 0;
        using (var scope = new FixtureScope(dependency))
            CheckNull("bare instruction with modreq", BundleCases.Pack(plainInstruction, dependency),
                scope.Entry(plainInstruction));

        byte[] bareSignature = (byte[])image.Clone();
        bareSignature[signature] = 2;
        bareSignature[signature + 2] = 8;
        using (var scope = new FixtureScope(dependency))
        {
            var bare = scope.Entry(bareSignature);
            try
            {
                bare();
                throw new InvalidOperationException("The CLR accepted a bare signature for a volatile field.");
            }
            catch (MissingFieldException) { }
        }
        Reject("missing required modifier", BundleCases.Pack(bareSignature, dependency), read, -6);

        byte[] optional = (byte[])image.Clone();
        optional[signature + 2] = 0x20;
        Reject("unsupported optional modifier", BundleCases.Pack(optional, dependency), read, -2);

        var coreReference = metadata.GetAssemblyReference((AssemblyReferenceHandle)marker.ResolutionScope);
        int token = heap + MetadataTokens.GetHeapOffset(coreReference.PublicKeyOrToken);
        byte[] wrongScope = (byte[])image.Clone();
        wrongScope[token + 1] ^= 1;
        Reject("same marker name in wrong scope", BundleCases.Pack(wrongScope, dependency), read, -2);

        byte[] wrongFacade = (byte[])image.Clone();
        var facadeName = metadata.TypeDefinitions.Select(metadata.GetTypeDefinition)
            .Single(t => metadata.GetString(t.Name) == "ModifierScopeFixture").Namespace;
        int assemblyRow = MetadataTokens.GetRowNumber((AssemblyReferenceHandle)marker.ResolutionScope);
        int assembly = pe.PEHeaders.MetadataStartOffset + metadata.GetTableMetadataOffset(TableIndex.AssemblyRef) +
            (assemblyRow - 1) * metadata.GetTableRowSize(TableIndex.AssemblyRef);
        int nameAt = assembly + 12 + (metadata.GetHeapSize(HeapIndex.Blob) < 65536 ? 2 : 4);
        int nameIndex = MetadataTokens.GetHeapOffset(facadeName);
        if (metadata.GetHeapSize(HeapIndex.String) < 65536)
            BinaryPrimitives.WriteUInt16LittleEndian(wrongFacade.AsSpan(nameAt), checked((ushort)nameIndex));
        else
            BinaryPrimitives.WriteInt32LittleEndian(wrongFacade.AsSpan(nameAt), nameIndex);
        Convert.FromHexString("B03F5F7F11D50A3A").CopyTo(wrongFacade.AsSpan(token + 1));
        Reject("platform facade without marker", BundleCases.Pack(wrongFacade, dependency), read, -2);
        string readStatic = nameof(Kernels.ReadStatic);
        int staticPrefix = Offset(readStatic);
        string runStatic = nameof(Kernels.RunStatic);
        var (staticInputs, staticHex) = Compile(bundle, runStatic);
        foreach (int value in new[] { int.MinValue, -1, 0, 1, int.MaxValue })
            cases.Add(new
            {
                module = "BootstrapLibrary", method = "CompileBundle", fixture = "volatile static read and write order",
                inputs = staticInputs, generated = new { capacity, bundle = true, arguments = new[] { value }, hex = staticHex },
                expected = Kernels.RunStatic(value)
            });

        byte[] volatileAddress = (byte[])image.Clone();
        int staticRva = metadata.GetMethodDefinition(MetadataTokens.MethodDefinitionHandle(Row(readStatic))).RelativeVirtualAddress;
        var staticSection = pe.PEHeaders.SectionHeaders[pe.PEHeaders.GetContainingSectionIndex(staticRva)];
        int staticBody = staticSection.PointerToRawData + staticRva - staticSection.VirtualAddress;
        int staticHeader = (image[staticBody] & 3) == 2 ? 1 : (image[staticBody + 1] >> 4) * 4;
        // A volatile prefix applies to the read or write, not to taking a field address.
        volatileAddress[staticBody + staticHeader + staticPrefix + 2] = 0x7f;
        Reject("volatile static address", BundleCases.Pack(volatileAddress, dependency), readStatic, -2);

        void CheckNull(string label, byte[] input, Action clr)
        {
            var (inputs, hex) = Compile(input, read);
            try
            {
                clr();
                throw new InvalidOperationException($"The CLR did not throw in {label}.");
            }
            catch (NullReferenceException error)
            {
                string exception = error.GetType().FullName!;
                cases.Add(new
                {
                    module = "BootstrapLibrary", method = "CompileBundle", fixture = "volatile field " + label,
                    inputs, generated = new { capacity, bundle = true, arguments = new object[] { null }, hex, exceptionType = exception },
                    expected = (int?)null, exception
                });
            }
        }

        (object[] Inputs, string Hex) Compile(byte[] input, string name)
        {
            byte[] entries = Manifest(name);
            int status = CompilerExports.CompileBundleDetailed(input, entries, output, location);
            if (status <= 0)
                throw new InvalidOperationException($"{name} returned {status} at [{string.Join(", ", location)}].");
            return ([Encode(input), Encode(entries)], Convert.ToHexString(output.AsSpan(0, status)).ToLowerInvariant());
        }

        void Reject(string label, byte[] input, string name, int expected)
        {
            byte[] entries = Manifest(name);
            int[] diagnostic = [0, Row(name), Offset(name)];
            Array.Fill(output, (byte)0xa5);
            int status = CompilerExports.CompileBundleDetailed(input, entries, output, location);
            if (status != expected || !location.SequenceEqual(diagnostic) || output.Any(b => b != 0xa5))
                throw new InvalidOperationException($"{label} returned {status} at [{string.Join(", ", location)}].");
            cases.Add(new
            {
                module = "BootstrapLibrary", method = "CompileBundle", fixture = "volatile field " + label,
                inputs = new[] { Encode(input), Encode(entries) },
                generated = new { capacity, bundle = true, arguments = Array.Empty<int>(), reject = true, diagnostic },
                expected = status
            });
        }
    }

    private sealed class FixtureScope(byte[] dependency) : AssemblyLoadContext(isCollectible: true), IDisposable
    {
        internal Action Entry(byte[] image)
        {
            LoadFromStream(new MemoryStream(dependency));
            var method = LoadFromStream(new MemoryStream(image)).GetType(typeof(Kernels).FullName!)!
                .GetMethod(nameof(Kernels.Read))!;
            return () =>
            {
                try { method.Invoke(null, [null]); }
                catch (TargetInvocationException error) when (error.InnerException != null)
                {
                    System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error.InnerException).Throw();
                }
            };
        }

        public void Dispose() => Unload();
    }

    private static byte[] MethodIl(string name) => typeof(Kernels).GetMethod(name)!.GetMethodBody()!.GetILAsByteArray()!;
    private static int Row(string name) => typeof(Kernels).GetMethod(name)!.MetadataToken & 0xffffff;

    private static int Offset(string method)
    {
        int offset = IlOffsets.Find(MethodIl(method), OpCodes.Volatile);
        return offset >= 0 ? offset : throw new InvalidOperationException($"{method} did not emit volatile.");
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
