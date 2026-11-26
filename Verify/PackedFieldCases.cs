using System.Buffers.Binary;
using System.Globalization;
using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Runtime.Loader;
using BootstrapLibrary;
using PackedFieldDemo;
using PackedFieldDependency;

internal static class PackedFieldCases
{
    internal static void Add(List<object> cases)
    {
        const int capacity = 32768;
        byte[] image = File.ReadAllBytes(typeof(Kernels).Assembly.Location);
        byte[] dependency = File.ReadAllBytes(typeof(Register).Assembly.Location);
        byte[] bundle = BundleCases.Pack(image, dependency);
        byte[] output = new byte[capacity];
        int[] location = CompilerExports.CreateDiagnostic();
        int[] boundaries = [int.MinValue, -65537, -32769, -129, -128, -1, 0, 1, 127, 128, 255, 256,
            32767, 32768, 65535, 65536, int.MaxValue];
        Valid("inherited and local fields", bundle, nameof(Kernels.Run), boundaries, value => Kernels.Run(value));
        Valid("UInt32 width", bundle, nameof(Kernels.WidenWord), [int.MinValue, -1, 0, 1, int.MaxValue],
            value => new { i64 = Kernels.WidenWord(value).ToString(CultureInfo.InvariantCulture) });
        Valid("foreign UInt16 field", bundle, nameof(Kernels.Store), [-1, 0, 32768, 65535, 65536], value => Kernels.Store(value));
        AddNull(nameof(Kernels.NullRead), [null], () => Kernels.NullRead(null));
        AddNull(nameof(Kernels.NullWrite), [null, 3], () => { Kernels.NullWrite(null, 3); return 0; });

        using var pe = new PEReader(new MemoryStream(image));
        var metadata = pe.GetMetadataReader();
        byte[] truncate = (byte[])image.Clone();
        truncate[IlStart(nameof(Kernels.TruncateSigned)) + Offset(nameof(Kernels.TruncateSigned), OpCodes.Conv_I1)] =
            (byte)OpCodes.Nop.Value;
        using (var scope = new FixtureScope(dependency))
        {
            // Store an int32 directly to the int8 field. The CLR and WebAssembly
            // must truncate on the field store even without a separate conv.i1.
            var run = scope.Entry(truncate, nameof(Kernels.TruncateSigned));
            Valid("packed truncation", BundleCases.Pack(truncate, dependency), nameof(Kernels.TruncateSigned),
                boundaries, value =>
                {
                    int result = run(value);
                    return result == Kernels.TruncateSigned(value) ? result :
                        throw new InvalidOperationException("Changed CLR truncation.");
                });
        }

        byte[] wideStore = (byte[])image.Clone();
        BinaryPrimitives.WriteInt32LittleEndian(wideStore.AsSpan(IlStart(nameof(Kernels.Store)) +
            Offset(nameof(Kernels.Store), OpCodes.Call) + 1), typeof(Kernels).GetMethod(nameof(Kernels.WideValue))!.MetadataToken);
        Reject("wide stack slot", BundleCases.Pack(wideStore, dependency), OpCodes.Stfld, -1);

        byte[] signedSignature = (byte[])image.Clone();
        int token = BinaryPrimitives.ReadInt32LittleEndian(image.AsSpan(IlStart(nameof(Kernels.Store)) +
            Offset(nameof(Kernels.Store), OpCodes.Stfld) + 1));
        var reference = metadata.GetMemberReference((MemberReferenceHandle)MetadataTokens.Handle(token));
        int blob = pe.PEHeaders.MetadataStartOffset + metadata.GetHeapMetadataOffset(HeapIndex.Blob) +
            MetadataTokens.GetHeapOffset(reference.Signature);
        if (!signedSignature.AsSpan(blob, 3).SequenceEqual(new byte[] { 2, 6, 7 }))
            throw new InvalidOperationException("Missing UInt16 field signature.");
        signedSignature[blob + 2] = 6; // Force a reference to Int16 instead of UInt16.
        using (var scope = new FixtureScope(dependency))
        {
            var run = scope.Entry(signedSignature, nameof(Kernels.Store));
            try
            {
                run(3);
                throw new InvalidOperationException("The CLR bound a field with mismatched signedness.");
            }
            catch (MissingFieldException) { }
        }
        Reject("signedness mismatch", BundleCases.Pack(signedSignature, dependency), OpCodes.Stfld, -6);

        void Valid(string label, byte[] input, string method, int[] values, Func<int, object> clr)
        {
            var (inputs, hex) = Compile(input, method);
            foreach (int value in values)
                cases.Add(new
                {
                    module = "BootstrapLibrary", method = "CompileBundle", fixture = "packed field " + label,
                    inputs, generated = new { capacity, bundle = true, arguments = new[] { value }, hex }, expected = clr(value)
                });
        }

        void AddNull(string name, object[] arguments, Func<int> clr)
        {
            var (inputs, hex) = Compile(bundle, name);
            try
            {
                clr();
                throw new InvalidOperationException($"CLR did not throw in {name}.");
            }
            catch (NullReferenceException error)
            {
                string exception = error.GetType().FullName!;
                cases.Add(new
                {
                    module = "BootstrapLibrary", method = "CompileBundle", fixture = "packed field " + name,
                    inputs, generated = new { capacity, bundle = true, arguments, hex, exceptionType = exception },
                    expected = (int?)null, exception
                });
            }
        }

        (object[] Inputs, string Hex) Compile(byte[] input, string name)
        {
            byte[] entries = Manifest(name);
            int result = CompilerExports.CompileBundleDetailed(input, entries, output, location);
            if (result <= 0)
                throw new InvalidOperationException($"{name} returned {result} at [{string.Join(", ", location)}].");
            return ([Encode(input), Encode(entries)], Convert.ToHexString(output.AsSpan(0, result)).ToLowerInvariant());
        }

        void Reject(string label, byte[] input, OpCode opcode, int expected)
        {
            string name = nameof(Kernels.Store);
            byte[] entries = Manifest(name);
            int[] diagnostic = [0, Row(name), Offset(name, opcode)];
            Array.Fill(output, (byte)0xa5);
            int status = CompilerExports.CompileBundleDetailed(input, entries, output, location);
            if (status != expected || !location.SequenceEqual(diagnostic) || output.Any(b => b != 0xa5))
                throw new InvalidOperationException($"{label} returned {status} at [{string.Join(", ", location)}].");
            cases.Add(new
            {
                module = "BootstrapLibrary", method = "CompileBundle", fixture = "packed field " + label,
                inputs = new[] { Encode(input), Encode(entries) },
                generated = new { capacity, bundle = true, arguments = Array.Empty<int>(), reject = true, diagnostic },
                expected = status
            });
        }

        int IlStart(string method)
        {
            int rva = metadata.GetMethodDefinition(MetadataTokens.MethodDefinitionHandle(Row(method))).RelativeVirtualAddress;
            var section = pe.PEHeaders.SectionHeaders[pe.PEHeaders.GetContainingSectionIndex(rva)];
            int origin = section.PointerToRawData + rva - section.VirtualAddress;
            return origin + ((image[origin] & 3) == 2 ? 1 : (image[origin + 1] >> 4) * 4);
        }
    }

    private sealed class FixtureScope(byte[] dependency) : AssemblyLoadContext(isCollectible: true), IDisposable
    {
        internal Func<int, int> Entry(byte[] image, string method) =>
            LoadFromStream(new MemoryStream(image)).GetType(typeof(Kernels).FullName!)!.GetMethod(method)!
                .CreateDelegate<Func<int, int>>();

        protected override Assembly Load(AssemblyName name) =>
            name.Name == "PackedFieldDependency" ? LoadFromStream(new MemoryStream(dependency)) : null;

        public void Dispose() => Unload();
    }

    private static int Row(string method) => typeof(Kernels).GetMethod(method)!.MetadataToken & 0xffffff;

    private static int Offset(string method, OpCode opcode)
    {
        byte[] il = typeof(Kernels).GetMethod(method)!.GetMethodBody()!.GetILAsByteArray()!;
        int offset = IlOffsets.Find(il, opcode);
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
