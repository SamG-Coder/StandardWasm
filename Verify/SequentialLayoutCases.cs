using System.Buffers.Binary;
using System.Globalization;
using System.Reflection;
using BootstrapLibrary;
using SequentialLayoutDemo;
using SequentialLayoutDependency;

// Stage 115: sequential reference layouts in an ordinary linked dependency.
internal static class SequentialLayoutCases
{
    internal static void Add(List<object> cases)
    {
        const int capacity = 65536;
        byte[] bundle = BundleCases.Pack(File.ReadAllBytes(typeof(Kernels).Assembly.Location),
            File.ReadAllBytes(typeof(Shape).Assembly.Location));
        byte[] output = new byte[capacity];
        int[] location = CompilerExports.CreateDiagnostic();
        var compiled = new Dictionary<string, (byte[] Entries, string Hex)>();

        int[] samples = [int.MinValue, -70000, -1, 0, 3, 255, 65537, int.MaxValue];
        foreach (int width in samples)
            foreach (int height in new[] { -5, 0, 7, 40000 })
            {
                Check(nameof(Kernels.SealedArea), [width, height], Kernels.SealedArea(width, height));
                Check(nameof(Kernels.BaseArea), [width, height], Kernels.BaseArea(width, height));
                Check(nameof(Kernels.BaseWeight), [height, width], Kernels.BaseWeight(height, width));
                Check(nameof(Kernels.LedgerFields), [width, height], Kernels.LedgerFields(width, height));
                Check(nameof(Kernels.AccountFields), [width, height], Kernels.AccountFields(width, height));
            }
        Check(nameof(Kernels.DefaultLedger), [], Kernels.DefaultLedger());

        // The CLR refuses the sequential-from-auto shape at type load. The
        // other rejected shapes load on the CLR but need byte or initializer
        // semantics this stage does not provide.
        try
        {
            Kernels.RejectFromAuto(1);
            throw new InvalidOperationException("CLR loaded a sequential class derived from an auto class.");
        }
        catch (TypeLoadException)
        {
        }
        try
        {
            _ = typeof(Shape).Assembly.GetType("SequentialLayoutDependency.ImportedSequential", throwOnError: true);
            throw new InvalidOperationException("CLR loaded the COM-imported sequential class.");
        }
        catch (TypeLoadException)
        {
        }
        Reject(nameof(Kernels.ReadImported), -1);

        if (Kernels.RejectInitialized(1) != 43 || Kernels.RejectPacked(2) != 2 || Kernels.RejectSized(3) != 3 ||
            Kernels.RejectOverlaid(4) != 4)
            throw new InvalidOperationException("CLR did not accept the rejected layout fixture types.");
        // Stage 129 (decision 0057): the precise initializer runs at construction.
        Check(nameof(Kernels.RejectInitialized), [1], Kernels.RejectInitialized(1));
        foreach (string name in new[] { nameof(Kernels.RejectFromAuto),
            nameof(Kernels.RejectPacked), nameof(Kernels.RejectSized), nameof(Kernels.RejectOverlaid) })
            Reject(name);

        void Check(string name, object[] arguments, object expected)
        {
            if (!compiled.TryGetValue(name, out var fixture))
            {
                byte[] entries = Manifest(Method(name));
                int length = CompilerExports.CompileBundleDetailed(bundle, entries, output, location);
                if (length < 1) throw new InvalidOperationException($"{name} returned {length} at [{string.Join(", ", location)}].");
                fixture = (entries, Convert.ToHexString(output.AsSpan(0, length)).ToLowerInvariant());
                compiled.Add(name, fixture);
            }
            cases.Add(new
            {
                module = "BootstrapLibrary", method = "CompileBundle", fixture = "sequential layout " + name,
                inputs = new[] { Encode(bundle), Encode(fixture.Entries) },
                generated = new { capacity, bundle = true, arguments, hex = fixture.Hex },
                expected
            });
        }

        // Rejections identify the newobj or the method signature that reaches the type.
        void Reject(string name, int ilOffset = 0)
        {
            MethodInfo method = Method(name);
            byte[] entries = Manifest(method);
            int[] diagnostic = [0, Row(method), ilOffset];
            Array.Fill(output, (byte)0xa5);
            int status = CompilerExports.CompileBundleDetailed(bundle, entries, output, location);
            if (status != -2 || !location.SequenceEqual(diagnostic) || output.Any(value => value != 0xa5))
                throw new InvalidOperationException($"{name} returned {status} at [{string.Join(", ", location)}].");
            cases.Add(new
            {
                module = "BootstrapLibrary", method = "CompileBundle", fixture = "sequential layout " + name,
                inputs = new[] { Encode(bundle), Encode(entries) },
                generated = new { capacity, bundle = true, arguments = new object[] { 1 }, reject = true, diagnostic },
                expected = status
            });
        }
    }

    private static MethodInfo Method(string name) => typeof(Kernels).GetMethod(name)!;
    private static int Row(MethodInfo method) => method.MetadataToken & 0xffffff;

    private static byte[] Manifest(MethodInfo method)
    {
        byte[] entries = new byte[11];
        BinaryPrimitives.WriteInt32LittleEndian(entries, Row(method));
        entries[4] = 3;
        "run"u8.CopyTo(entries.AsSpan(8));
        return entries;
    }

    private static object Encode(byte[] bytes) => new { bytes = Array.ConvertAll(bytes, value => (int)value) };
}
