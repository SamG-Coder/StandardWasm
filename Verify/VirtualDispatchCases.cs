using System.Buffers.Binary;
using System.Reflection;
using System.Reflection.Emit;
using BootstrapLibrary;
using VirtualDispatchDemo;
using VirtualDispatchDependency;

internal static class VirtualDispatchCases
{
    internal static void Add(List<object> cases)
    {
        const int capacity = 65536;
        byte[] bundle = BundleCases.Pack(File.ReadAllBytes(typeof(Kernels).Assembly.Location),
            File.ReadAllBytes(typeof(Root).Assembly.Location));
        byte[] output = new byte[capacity];
        int[] location = CompilerExports.CreateDiagnostic();

        Check(nameof(Kernels.RunInherited), Kernels.RunInherited);
        Check(nameof(Kernels.RunDeclared), Kernels.RunDeclared);
        Check(nameof(Kernels.RunHiddenBase), Kernels.RunHiddenBase);
        Check(nameof(Kernels.RunIntroducedSlot), Kernels.RunIntroducedSlot);
        Check(nameof(Kernels.RunNull), Kernels.RunNull);
        if (Kernels.RejectOpen(new Inherited()) != 20 || Kernels.RejectOpen(new Root()) != 10)
            throw new InvalidOperationException("The CLR did not dispatch the open receiver.");

        MethodInfo open = typeof(Kernels).GetMethod(nameof(Kernels.RejectOpen))!;
        byte[] rejected = Manifest(open);
        int call = IlOffsets.Find(open.GetMethodBody()!.GetILAsByteArray()!, OpCodes.Callvirt);
        int[] diagnostic = [0, Row(open), call];
        Array.Fill(output, (byte)0xa5);
        int status = CompilerExports.CompileBundleDetailed(bundle, rejected, output, location);
        if (call < 0 || status != -2 || !location.SequenceEqual(diagnostic) || output.Any(value => value != 0xa5))
            throw new InvalidOperationException($"Open receiver returned {status} at [{string.Join(", ", location)}].");
        cases.Add(new
        {
            module = "BootstrapLibrary", method = "CompileBundle", fixture = "exact virtual open receiver",
            inputs = new[] { Encode(bundle), Encode(rejected) },
            generated = new { capacity, bundle = true, arguments = Array.Empty<int>(), reject = true, diagnostic },
            expected = status
        });

        void Check(string name, Func<int, int> oracle)
        {
            byte[] entries = Manifest(typeof(Kernels).GetMethod(name)!);
            int length = CompilerExports.CompileBundleDetailed(bundle, entries, output, location);
            if (length < 1) throw new InvalidOperationException($"{name} returned {length} at [{string.Join(", ", location)}].");
            string hex = Convert.ToHexString(output.AsSpan(0, length)).ToLowerInvariant();
            foreach (int input in new[] { int.MinValue, -1, 0, 5, int.MaxValue })
                cases.Add(new
                {
                    module = "BootstrapLibrary", method = "CompileBundle", fixture = "exact virtual " + name,
                    inputs = new[] { Encode(bundle), Encode(entries) },
                    generated = new { capacity, bundle = true, arguments = new[] { input }, hex },
                    expected = oracle(input)
                });
        }
    }

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
