using System.Buffers.Binary;
using System.Globalization;
using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Runtime.Loader;
using BootstrapLibrary;
using EnumFieldDemo;
using EnumFieldDependency;

internal static class EnumFieldCases
{
    internal static void Add(List<object> cases)
    {
        const int capacity = 32768;
        byte[] image = File.ReadAllBytes(typeof(Kernels).Assembly.Location);
        byte[] dependency = File.ReadAllBytes(typeof(Frame).Assembly.Location);
        byte[] bundle = BundleCases.Pack(image, dependency);
        byte[] output = new byte[capacity];
        int[] location = CompilerExports.CreateDiagnostic();

        Compare(nameof(Kernels.Run), new[] { int.MinValue, -65537, -32769, -129, -128, -1, 0, 1,
            127, 128, 255, 256, 32767, 32768, 65535, 65536, int.MaxValue }, value => value, value => Kernels.Run(value));
        Compare(nameof(Kernels.ScoreOther), new[] { int.MinValue, -1, 0, 8, int.MaxValue }, value => value,
            value => Kernels.ScoreOther(value));
        Compare(nameof(Kernels.WidenWord), new[] { int.MinValue, -1, 0, 1, int.MaxValue }, value => value,
            value => Wide(Kernels.WidenWord(value)));
        Compare(nameof(Kernels.RoundTripWide), new[] { long.MinValue, -4294967297L, -1, 0, 1, 4294967297L, long.MaxValue },
            Wide, value => Wide(Kernels.RoundTripWide(value)));

        Compare(nameof(Kernels.EnumVectors), new[] { 0, 1, 2, 3, 4 }, value => value, value => Kernels.EnumVectors(value));

        string read = nameof(Kernels.Read);
        var (readInputs, readHex) = Compile(read);
        try
        {
            Kernels.Read(null);
            throw new InvalidOperationException("The CLR did not check the enum field receiver.");
        }
        catch (NullReferenceException error)
        {
            string exception = error.GetType().FullName!;
            cases.Add(new
            {
                module = "BootstrapLibrary", method = "CompileBundle", fixture = "nominal enum null receiver", inputs = readInputs,
                generated = new { capacity, bundle = true, arguments = new object[] { null }, hex = readHex, exceptionType = exception },
                expected = (int?)null, exception
            });
        }

        using var pe = new PEReader(new MemoryStream(image));
        var metadata = pe.GetMetadataReader();
        int load = Offset(read, OpCodes.Ldfld);
        int token = BinaryPrimitives.ReadInt32LittleEndian(IL(read).AsSpan(load + 1));
        var member = metadata.GetMemberReference((MemberReferenceHandle)MetadataTokens.Handle(token));
        var signature = metadata.GetBlobReader(member.Signature);
        if (signature.ReadByte() != 6 || signature.ReadByte() != 0x11 ||
            metadata.GetString(metadata.GetTypeReference((TypeReferenceHandle)signature.ReadTypeHandle()).Name) != "States" ||
            signature.RemainingBytes != 0)
            throw new InvalidOperationException("The linked fixture did not encode an enum field.");
        var other = metadata.TypeReferences.Single(h => metadata.GetString(metadata.GetTypeReference(h).Name) == "OtherStates");
        var replacement = new BlobBuilder();
        replacement.WriteCompressedInteger(MetadataTokens.GetRowNumber(other) << 2 | 1);
        int offset = pe.PEHeaders.MetadataStartOffset + metadata.GetHeapMetadataOffset(HeapIndex.Blob) +
            MetadataTokens.GetHeapOffset(member.Signature);
        if (image[offset] != 2 + replacement.Count)
            throw new InvalidOperationException("The fixture needs equal-width TypeRef encodings.");
        byte[] mismatch = (byte[])image.Clone();
        replacement.ToArray().CopyTo(mismatch.AsSpan(offset + 3));
        using (var scope = new FixtureScope(dependency))
        {
            var readChanged = scope.Entry(mismatch);
            try
            {
                readChanged();
                throw new InvalidOperationException("The CLR accepted a different enum in the field signature.");
            }
            catch (MissingFieldException) { }
        }
        Reject("different same-width field signature", BundleCases.Pack(mismatch, dependency), read, load, -6);
        string box = nameof(Kernels.RejectBox);
        if (Kernels.RejectBox(8) is not States.Control)
            throw new InvalidOperationException("The CLR boxing fixture changed.");
        Reject("boxing", bundle, box, Offset(box, OpCodes.Box), -2);

        void Compare<T>(string name, T[] values, Func<T, object> argument, Func<T, object> clr)
        {
            var (inputs, hex) = Compile(name);
            foreach (T value in values)
                cases.Add(new
                {
                    module = "BootstrapLibrary", method = "CompileBundle", fixture = "nominal enum " + name, inputs,
                    generated = new { capacity, bundle = true, arguments = new[] { argument(value) }, hex },
                    expected = clr(value)
                });
        }

        (object[] Inputs, string Hex) Compile(string name)
        {
            byte[] entries = Manifest(name);
            int size = CompilerExports.CompileBundleDetailed(bundle, entries, output, location);
            if (size <= 0)
                throw new InvalidOperationException($"{name} returned {size} at [{string.Join(", ", location)}].");
            return ([Encode(bundle), Encode(entries)], Convert.ToHexString(output.AsSpan(0, size)).ToLowerInvariant());
        }

        void Reject(string label, byte[] input, string name, int il, int expected)
        {
            byte[] entries = Manifest(name);
            int[] diagnostic = [0, Row(name), il];
            Array.Fill(output, (byte)0xa5);
            int status = CompilerExports.CompileBundleDetailed(input, entries, output, location);
            if (status != expected || !location.SequenceEqual(diagnostic) || output.Any(b => b != 0xa5))
                throw new InvalidOperationException($"{label} returned {status} at [{string.Join(", ", location)}].");
            cases.Add(new
            {
                module = "BootstrapLibrary", method = "CompileBundle", fixture = "nominal enum " + label,
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

    private static byte[] IL(string name) => typeof(Kernels).GetMethod(name)!.GetMethodBody()!.GetILAsByteArray()!;
    private static int Row(string name) => typeof(Kernels).GetMethod(name)!.MetadataToken & 0xffffff;

    private static int Offset(string method, OpCode opcode)
    {
        int offset = IlOffsets.Find(IL(method), opcode);
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
    private static object Wide(long bits) => new { i64 = bits.ToString(CultureInfo.InvariantCulture) };
}
