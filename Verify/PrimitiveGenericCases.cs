using System.Buffers.Binary;
using System.Globalization;
using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Runtime.Loader;
using BootstrapLibrary;
using PrimitiveGenericDependency;
using PrimitiveGenericDemo;

internal static class PrimitiveGenericCases
{
    internal static void Add(List<object> cases)
    {
        const int capacity = 65536;
        byte[] image = File.ReadAllBytes(typeof(Kernels).Assembly.Location);
        byte[] dependency = File.ReadAllBytes(typeof(Frame).Assembly.Location);
        byte[] bundle = BundleCases.Pack(image, dependency);
        byte[] output = new byte[capacity];
        int[] location = CompilerExports.CreateDiagnostic();
        var compiled = new Dictionary<string, (object[] Inputs, string Hex)>();

        foreach (int value in new[] { int.MinValue, -5, 0, 5, int.MaxValue })
            Check(nameof(Kernels.Run), [value], () => Kernels.Run(value));
        foreach (int value in new[] { -129, -128, -1, 0, 127, 128, 255, int.MaxValue })
            Check(nameof(Kernels.Narrow), [value], () => Kernels.Narrow(value));
        foreach (double value in new[] { 0.0, -0.0, double.Epsilon, 0.1, double.PositiveInfinity, double.NaN })
            Check(nameof(Kernels.Measure), [Real(value)], () => Real(Kernels.Measure(value)));
        Check(nameof(Kernels.Read), [null], () => Kernels.Read(null));
        RejectChangedField(nameof(Kernels.Read), vector: false);
        RejectChangedField(nameof(Kernels.ReadVector), vector: true);

        void Check(string name, object[] arguments, Func<object> oracle)
        {
            if (!compiled.TryGetValue(name, out var fixture))
            {
                byte[] entries = Manifest(name);
                int length = CompilerExports.CompileBundleDetailed(bundle, entries, output, location);
                if (length <= 0)
                    throw new InvalidOperationException($"{name} returned {length} at [{string.Join(", ", location)}].");
                fixture = ([Encode(bundle), Encode(entries)], Convert.ToHexString(output.AsSpan(0, length)).ToLowerInvariant());
                compiled.Add(name, fixture);
            }
            object expected = null;
            string exception = null;
            try { expected = oracle(); }
            catch (NullReferenceException error) { exception = error.GetType().FullName; }
            cases.Add(new
            {
                module = "BootstrapLibrary", method = "CompileBundle", fixture = "primitive generic " + name,
                inputs = fixture.Inputs, generated = new { capacity, bundle = true, arguments, hex = fixture.Hex, exceptionType = exception },
                expected, exception
            });
        }

        void RejectChangedField(string name, bool vector)
        {
            var kernel = typeof(Kernels).GetMethod(name)!;
            byte[] il = kernel.GetMethodBody()!.GetILAsByteArray()!;
            OpCode opcode = vector ? OpCodes.Ldflda : OpCodes.Ldfld;
            int load = IlOffsets.Find(il, opcode);
            if (load < 0) throw new InvalidOperationException($"Missing field access in {name}.");
            int token = BinaryPrimitives.ReadInt32LittleEndian(il.AsSpan(load + 1));
            byte[] changed = ChangeArgument(image, token, vector);
            AssertClrFieldMismatch(changed, dependency, name);

            byte[] input = BundleCases.Pack(changed, dependency), entries = Manifest(name);
            int[] diagnostic = [0, Row(name), load];
            Array.Fill(output, (byte)0xa5);
            int status = CompilerExports.CompileBundleDetailed(input, entries, output, location);
            if (status != -6 || !location.SequenceEqual(diagnostic) || output.Any(value => value != 0xa5))
                throw new InvalidOperationException($"{name} mismatch returned {status} at [{string.Join(", ", location)}].");
            cases.Add(new
            {
                module = "BootstrapLibrary", method = "CompileBundle", fixture = "primitive generic wrong identity " + name,
                inputs = new[] { Encode(input), Encode(entries) },
                generated = new { capacity, bundle = true, arguments = Array.Empty<int>(), reject = true, diagnostic },
                expected = status
            });
        }
    }

    private static byte[] ChangeArgument(byte[] image, int token, bool vector)
    {
        using var pe = new PEReader(new MemoryStream(image));
        var metadata = pe.GetMetadataReader();
        var member = metadata.GetMemberReference((MemberReferenceHandle)MetadataTokens.Handle(token));
        var signature = metadata.GetBlobReader(member.Signature);
        if (signature.ReadByte() != 6 || signature.ReadByte() != 0x15 || signature.ReadByte() != 0x11 ||
            signature.ReadTypeHandle().Kind != HandleKind.TypeReference || signature.ReadCompressedInteger() != 2 ||
            (vector && signature.ReadByte() != 0x1d))
            throw new InvalidOperationException("Unexpected generic field signature.");
        int argument = signature.Offset;
        if (signature.ReadByte() != 8) throw new InvalidOperationException("Expected the signed I4 argument.");

        int heap = pe.PEHeaders.MetadataStartOffset + metadata.GetHeapMetadataOffset(HeapIndex.Blob);
        int blob = heap + MetadataTokens.GetHeapOffset(member.Signature);
        byte[] changed = (byte[])image.Clone();
        if (image[blob] != metadata.GetBlobBytes(member.Signature).Length)
            throw new InvalidOperationException("Expected a single-byte signature size.");
        changed[blob + 1 + argument] = 9; // Same Wasm width, different closed CLR identity.
        return changed;
    }

    private static void AssertClrFieldMismatch(byte[] image, byte[] dependency, string method)
    {
        var context = new AssemblyLoadContext("Primitive generic identity oracle", isCollectible: true);
        try
        {
            var frame = context.LoadFromStream(new MemoryStream(dependency)).GetType(typeof(Frame).FullName!)!;
            var kernel = context.LoadFromStream(new MemoryStream(image)).GetType(typeof(Kernels).FullName!)!.GetMethod(method)!;
            kernel.Invoke(null, [Activator.CreateInstance(frame)]);
            throw new InvalidOperationException($"{method} matched a different closed type on the CLR.");
        }
        catch (TargetInvocationException error) when (error.InnerException is MissingFieldException) { }
        finally { context.Unload(); }
    }

    private static int Row(string name) => typeof(Kernels).GetMethod(name)!.MetadataToken & 0xffffff;
    private static byte[] Manifest(string name)
    {
        byte[] entries = new byte[11];
        BinaryPrimitives.WriteInt32LittleEndian(entries, Row(name));
        entries[4] = 3;
        "run"u8.CopyTo(entries.AsSpan(8));
        return entries;
    }

    private static object Encode(byte[] bytes) => new { bytes = Array.ConvertAll(bytes, value => (int)value) };
    private static object Real(double value) => new { f64 = value.ToString("G17", CultureInfo.InvariantCulture) };
}
