using System.Buffers.Binary;
using System.Globalization;
using System.Reflection;
using BootstrapLibrary;
using ScalarOverlayDemo;
using ScalarOverlayDependency;

internal static class ExplicitScalarCases
{
    internal static void Add(List<object> cases)
    {
        const int capacity = 65536;
        byte[] bundle = BundleCases.Pack(File.ReadAllBytes(typeof(Kernels).Assembly.Location),
            File.ReadAllBytes(typeof(Overlay).Assembly.Location));
        byte[] output = new byte[capacity];
        int[] location = CompilerExports.CreateDiagnostic();
        var compiled = new Dictionary<string, (byte[] Entries, string Hex)>();

        int seed = 0x12345678;
        foreach (int value in new[] { int.MinValue, -32769, -32768, -2, 0, 32767, 32768, int.MaxValue })
            Check(nameof(Kernels.MiddleWrite), [seed, value], Kernels.MiddleWrite(seed, value));
        foreach (int bits in new[] { int.MinValue, -1, 0, seed, int.MaxValue })
        {
            Check(nameof(Kernels.ReadMiddle), [bits], Kernels.ReadMiddle(bits));
            Check(nameof(Kernels.SignedHigh), [bits], Kernels.SignedHigh(bits));
            Check(nameof(Kernels.CopyAndWrite), [bits, -2], Kernels.CopyAndWrite(bits, -2));
        }
        Check(nameof(Kernels.DefaultParts), [], Kernels.DefaultParts());
        Check(nameof(Kernels.ManagedDefault), [], Kernels.ManagedDefault());
        Check(nameof(Kernels.ManagedAlias), [17, -20], Kernels.ManagedAlias(17, -20));
        Check(nameof(Kernels.ManagedAlias), [int.MinValue, 0], Kernels.ManagedAlias(int.MinValue, 0));
        Check(nameof(Kernels.FrameNumericSlots), [-2, 3], Wide(Kernels.FrameNumericSlots(-2, 3)));
        Check(nameof(Kernels.FrameNumericSlots), [int.MinValue, int.MaxValue], Wide(Kernels.FrameNumericSlots(int.MinValue, int.MaxValue)));
        Check(nameof(Kernels.FrameReferenceSlots), [], Kernels.FrameReferenceSlots());
        Check(nameof(Kernels.ShortReferenceSlots), [], Kernels.ShortReferenceSlots());

        foreach (int bits in new[] { int.MinValue, 0, 1, 0x3f800000, 0x7f800000, 0x7fc12345 })
            Check(nameof(Kernels.SingleBits), [bits], Kernels.SingleBits(bits));
        foreach (long bits in new[] { long.MinValue, -1L, 0L, 1L, 0x123456789abcdefL, 0x7ff8123456789abcL })
        {
            Check(nameof(Kernels.DoubleBits), [Wide(bits)], Wide(Kernels.DoubleBits(bits)));
            Check(nameof(Kernels.CrossWord), [Wide(bits), -2], Wide(Kernels.CrossWord(bits, -2)));
            Check(nameof(Kernels.UnsignedHigh), [Wide(bits)], Kernels.UnsignedHigh(bits));
        }

        if (Kernels.RejectLarge(new LargeFrame()) != 1 || Kernels.RejectNested(new NestedFrame()) != 1 ||
            Kernels.RejectWidePartial(new WidePartialFrame()) != 1)
            throw new InvalidOperationException("CLR did not accept the explicit layout fixture types.");
        Reject(nameof(Kernels.RejectLarge));
        Reject(nameof(Kernels.RejectNested));
        Reject(nameof(Kernels.RejectWidePartial));

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
                module = "BootstrapLibrary", method = "CompileBundle", fixture = "explicit scalar " + name,
                inputs = new[] { Encode(bundle), Encode(fixture.Entries) },
                generated = new { capacity, bundle = true, arguments, hex = fixture.Hex },
                expected
            });
        }

        void Reject(string name)
        {
            MethodInfo method = Method(name);
            byte[] entries = Manifest(method);
            int[] diagnostic = [0, Row(method), -1];
            Array.Fill(output, (byte)0xa5);
            int status = CompilerExports.CompileBundleDetailed(bundle, entries, output, location);
            if (status != -2 || !location.SequenceEqual(diagnostic) || output.Any(value => value != 0xa5))
                throw new InvalidOperationException($"{name} returned {status} at [{string.Join(", ", location)}].");
            cases.Add(new
            {
                module = "BootstrapLibrary", method = "CompileBundle", fixture = "explicit scalar " + name,
                inputs = new[] { Encode(bundle), Encode(entries) },
                generated = new { capacity, bundle = true, arguments = Array.Empty<int>(), reject = true, diagnostic },
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
    private static object Wide(long bits) => new { i64 = bits.ToString(CultureInfo.InvariantCulture) };
}
