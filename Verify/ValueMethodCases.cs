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

        // A field store through the receiver must be visible to its caller.
        foreach (int value in samples)
            Check(nameof(Kernels.RejectStore), [value], Kernels.RejectStore(value));

        // Keep coverage of unsupported whole-receiver updates separately.
        if (Kernels.RejectStore(5) != 5 || Kernels.RejectReset(5) != 0 || Kernels.RejectReplace(5) != 5)
            throw new InvalidOperationException("CLR did not run the mutating value methods.");
        byte[] reset = typeof(Counter).GetMethod(nameof(Counter.Reset))!.GetMethodBody()!.GetILAsByteArray()!;
        int resetOffset = IlOffsets.Find(reset, OpCodes.Initobj);
        if (resetOffset < 0) throw new InvalidOperationException("Counter.Reset did not emit initobj.");
        Reject(nameof(Kernels.RejectReset), nameof(Counter.Reset), resetOffset);
        Reject(nameof(Kernels.RejectReplace), nameof(Counter.Replace), 7);

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

        // Each rejection is located at the mutating instruction in the callee,
        // which is assembly 1 of the bundle.
        void Reject(string name, string callee, int offset)
        {
            MethodInfo method = Method(name);
            byte[] entries = Manifest(method);
            int[] diagnostic = [1, typeof(Counter).GetMethod(callee)!.MetadataToken & 0xffffff, offset];
            Array.Fill(output, (byte)0xa5);
            int status = CompilerExports.CompileBundleDetailed(bundle, entries, output, location);
            if (status != -2 || !location.SequenceEqual(diagnostic) || output.Any(value => value != 0xa5))
                throw new InvalidOperationException($"{name} returned {status} at [{string.Join(", ", location)}].");
            cases.Add(new
            {
                module = "BootstrapLibrary", method = "CompileBundle", fixture = "value method " + name,
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
