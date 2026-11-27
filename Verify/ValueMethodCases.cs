using System.Buffers.Binary;
using System.Globalization;
using System.Reflection;
using System.Reflection.Emit;
using BootstrapLibrary;
using ValueMethodDemo;
using ValueMethodDependency;

// Stage 121: read-only value-type instance methods in an ordinary linked dependency.
internal static class ValueMethodCases
{
    internal static void Add(List<object> cases)
    {
        const int capacity = 65536;
        byte[] bundle = BundleCases.Pack(File.ReadAllBytes(typeof(Kernels).Assembly.Location),
            File.ReadAllBytes(typeof(Extent).Assembly.Location));
        byte[] output = new byte[capacity];
        int[] location = CompilerExports.CreateDiagnostic();
        var compiled = new Dictionary<string, (byte[] Entries, string Hex)>();

        int[] samples = [int.MinValue, -70000, -1, 0, 3, 255, 65537, int.MaxValue];
        foreach (int first in samples)
            foreach (int second in new[] { -5, 0, 7, 40000 })
            {
                Check(nameof(Kernels.LinkedGetters), [first, second], Kernels.LinkedGetters(first, second));
                Check(nameof(Kernels.LinkedChain), [first, second], Kernels.LinkedChain(first, second));
                Check(nameof(Kernels.CopyThenWrite), [first, second], Kernels.CopyThenWrite(first, second));
                Check(nameof(Kernels.ConstructorCall), [first, second], Kernels.ConstructorCall(first, second));
                foreach (int probe in new[] { int.MinValue, -1, 0, 6, int.MaxValue })
                    Check(nameof(Kernels.LocalSpan), [first, second, probe], Kernels.LocalSpan(first, second, probe));
            }
        Check(nameof(Kernels.DefaultReceiver), [], Kernels.DefaultReceiver());

        // Ensure the reset case exercises the value-at-address initialization instruction.
        byte[] reset = typeof(Counter).GetMethod(nameof(Counter.Reset))!.GetMethodBody()!.GetILAsByteArray()!;
        if (IlOffsets.Find(reset, OpCodes.Initobj) < 0)
            throw new InvalidOperationException("Counter.Reset did not emit initobj.");
        byte[] replace = typeof(Counter).GetMethod(nameof(Counter.Replace))!.GetMethodBody()!.GetILAsByteArray()!;
        if (IlOffsets.Find(replace, OpCodes.Stobj) < 0)
            throw new InvalidOperationException("Counter.Replace did not emit stobj.");
        foreach (int value in samples)
        {
            Check(nameof(Kernels.RejectStore), [value], Kernels.RejectStore(value));
            Check(nameof(Kernels.ResetReceiver), [value], Kernels.ResetReceiver(value));
            Check(nameof(Kernels.ReplaceReceiver), [value], Kernels.ReplaceReceiver(value));
        }

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
                module = "BootstrapLibrary", method = "CompileBundle", fixture = "value method " + name,
                inputs = new[] { Encode(bundle), Encode(fixture.Entries) },
                generated = new { capacity, bundle = true, arguments, hex = fixture.Hex },
                expected
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
