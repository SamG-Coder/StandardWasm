using System.Buffers.Binary;
using System.Globalization;
using System.Reflection;
using BootstrapLibrary;
using NestedOverlayDemo;
using NestedOverlayDependency;

internal static class NestedOverlayCases
{
    internal static void Add(List<object> cases)
    {
        const int capacity = 65536;
        byte[] bundle = BundleCases.Pack(File.ReadAllBytes(typeof(Kernels).Assembly.Location),
            File.ReadAllBytes(typeof(Overlay).Assembly.Location));
        byte[] output = new byte[capacity];
        int[] location = CompilerExports.CreateDiagnostic();
        var compiled = new Dictionary<string, (byte[] Entries, string Hex)>();

        foreach (long bits in new[] { long.MinValue, -1L, 0L, 1L, 0x123456789abcdefL, long.MaxValue })
        {
            Check(nameof(Kernels.ReadPair), [Wide(bits)], Kernels.ReadPair(bits));
            Check(nameof(Kernels.UpdatePair), [Wide(bits), int.MinValue], Wide(Kernels.UpdatePair(bits, int.MinValue)));
            Check(nameof(Kernels.ReadColor), [Wide(bits)], Kernels.ReadColor(bits));
            Check(nameof(Kernels.UpdateMiddle), [Wide(bits), -2], Wide(Kernels.UpdateMiddle(bits, -2)));
            Check(nameof(Kernels.WriteMiddle), [Wide(bits), -2], Wide(Kernels.WriteMiddle(bits, -2)));
            Check(nameof(Kernels.PackDefault), [Wide(bits)], Wide(Kernels.PackDefault(bits)));
        }
        foreach (int value in new[] { int.MinValue, -5, 0, 5, int.MaxValue })
            Check(nameof(Kernels.CopyAndRead), [value, 10], Kernels.CopyAndRead(value, 10));

        if (Kernels.RejectPadded(default) != 0 || Kernels.RejectMixedAuto(default) != 0)
            throw new InvalidOperationException("CLR rejected a nested layout fixture.");
        Reject(nameof(Kernels.RejectPadded));
        Reject(nameof(Kernels.RejectMixedAuto));
        const long paddingBits = unchecked((long)0xfedc000000000000UL);
        if (Kernels.RestoreAutoPadding(paddingBits) != paddingBits)
            throw new InvalidOperationException("CLR did not preserve the auto-layout view's padding.");
        Reject(nameof(Kernels.RestoreAutoPadding));

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
                module = "BootstrapLibrary", method = "CompileBundle", fixture = "nested overlay " + name,
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
                module = "BootstrapLibrary", method = "CompileBundle", fixture = "nested overlay " + name,
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
