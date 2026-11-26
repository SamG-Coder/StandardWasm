using System.Buffers.Binary;
using System.Globalization;
using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Runtime.Loader;
using BootstrapLibrary;
using FloatFieldDemo;
using FloatFieldDependency;

internal static class FloatFieldCases
{
    internal static void Add(List<object> cases)
    {
        const int capacity = 32768;
        byte[] image = File.ReadAllBytes(typeof(Kernels).Assembly.Location);
        byte[] dependency = File.ReadAllBytes(typeof(FontMetrics).Assembly.Location);
        byte[] bundle = BundleCases.Pack(image, dependency);
        byte[] output = new byte[capacity];
        var compiled = new Dictionary<string, (object[] Inputs, string Hex)>();

        foreach (double value in new[] { 0.0, -0.0, double.Epsilon, -double.Epsilon, 0.1, 16777217.0,
            double.MaxValue, double.NegativeInfinity, double.PositiveInfinity, double.NaN })
            Check(nameof(Kernels.DoubleRoundTrip), [Real(value)], () => Kernels.DoubleRoundTrip(value));

        foreach (float value in new[] { 0f, -0f, float.Epsilon, -float.Epsilon, 0.1f, 16777216f,
            float.MaxValue, float.NegativeInfinity, float.PositiveInfinity, float.NaN })
            Check(nameof(Kernels.SingleRoundTrip), [Real(value)], () => Kernels.SingleRoundTrip(value));

        Check(nameof(Kernels.DefaultSingle), [], () => Kernels.DefaultSingle());
        Check(nameof(Kernels.DefaultDouble), [], () => Kernels.DefaultDouble());
        Check(nameof(Kernels.Run), [16777217, 16777219], () => Kernels.Run(16777217, 16777219));
        Check(nameof(Kernels.CopyMeasure), [Real(0.1), Real(0.125f)], () => Kernels.CopyMeasure(0.1, 0.125f));
        Check(nameof(Kernels.CopyMeasure), [Real(double.Epsilon), Real(0.0)], () => Kernels.CopyMeasure(double.Epsilon, 0f));
        Check(nameof(Kernels.Read), [null], () => Kernels.Read(null));
        Check(nameof(Kernels.Write), [null, Real(1)], () => { Kernels.Write(null, 1); return 0; });

        RejectMismatchedField();

        void Check(string name, object[] arguments, Func<double> oracle)
        {
            if (!compiled.TryGetValue(name, out var fixture))
            {
                byte[] manifest = Manifest(name);
                int[] location = CompilerExports.CreateDiagnostic();
                int length = CompilerExports.CompileBundleDetailed(bundle, manifest, output, location);
                if (length <= 0)
                    throw new InvalidOperationException($"{name} returned {length} at [{string.Join(", ", location)}].");
                fixture = ([Encode(bundle), Encode(manifest)], Convert.ToHexString(output.AsSpan(0, length)).ToLowerInvariant());
                compiled.Add(name, fixture);
            }

            object expected = null;
            string exception = null;
            try { expected = Real(oracle()); }
            catch (NullReferenceException error) { exception = error.GetType().FullName; }
            cases.Add(new
            {
                module = "BootstrapLibrary", method = "CompileBundle", fixture = "float field " + name, inputs = fixture.Inputs,
                generated = new { capacity, bundle = true, arguments, hex = fixture.Hex, exceptionType = exception },
                expected, exception
            });
        }

        void RejectMismatchedField()
        {
            string name = nameof(Kernels.Read);
            byte[] il = typeof(Kernels).GetMethod(name)!.GetMethodBody()!.GetILAsByteArray()!;
            int load = IlOffsets.Find(il, OpCodes.Ldfld);
            if (load < 0) throw new InvalidOperationException("The fixture must read a linked field.");
            byte[] mismatch = ChangeFieldWidth(image, BinaryPrimitives.ReadInt32LittleEndian(il.AsSpan(load + 1)));
            AssertClrFieldMismatch(mismatch, dependency);
            byte[] changedBundle = BundleCases.Pack(mismatch, dependency);
            byte[] manifest = Manifest(name);
            int[] location = CompilerExports.CreateDiagnostic();
            int[] diagnostic = [0, Row(name), load];
            Array.Fill(output, (byte)0xa5);
            int status = CompilerExports.CompileBundleDetailed(changedBundle, manifest, output, location);
            if (status != -6 || !location.SequenceEqual(diagnostic) || output.Any(value => value != 0xa5))
                throw new InvalidOperationException($"Wrong field width returned {status} at [{string.Join(", ", location)}].");
            cases.Add(new
            {
                module = "BootstrapLibrary", method = "CompileBundle", fixture = "float field mismatched signature",
                inputs = new[] { Encode(changedBundle), Encode(manifest) },
                generated = new { capacity, bundle = true, arguments = Array.Empty<int>(), reject = true, diagnostic },
                expected = status
            });
        }
    }

    private static byte[] ChangeFieldWidth(byte[] image, int token)
    {
        using var pe = new PEReader(new MemoryStream(image));
        var metadata = pe.GetMetadataReader();
        var reference = metadata.GetMemberReference((MemberReferenceHandle)MetadataTokens.Handle(token));
        int offset = pe.PEHeaders.MetadataStartOffset + metadata.GetHeapMetadataOffset(HeapIndex.Blob) +
            MetadataTokens.GetHeapOffset(reference.Signature);
        if (!image.AsSpan(offset, 3).SequenceEqual(new byte[] { 2, 6, 0x0c }))
            throw new InvalidOperationException("The fixture must use an R4 field signature.");
        byte[] mismatch = (byte[])image.Clone();
        mismatch[offset + 2] = 0x0d;
        return mismatch;
    }

    private static void AssertClrFieldMismatch(byte[] image, byte[] dependency)
    {
        var context = new AssemblyLoadContext("Floating field signature oracle", isCollectible: true);
        try
        {
            var metrics = context.LoadFromStream(new MemoryStream(dependency)).GetType(typeof(FontMetrics).FullName!)!;
            var method = context.LoadFromStream(new MemoryStream(image)).GetType(typeof(Kernels).FullName!)!
                .GetMethod(nameof(Kernels.Read))!;
            method.Invoke(null, [Activator.CreateInstance(metrics)]);
            throw new InvalidOperationException("The CLR found the field with the wrong width.");
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
