using System.Buffers.Binary;
using System.Reflection;
using System.Reflection.Emit;
using BootstrapLibrary;
using StaticStorageDemo;
using StaticStorageDependency;

internal static class StaticStorageCases
{
    internal static void Add(List<object> cases)
    {
        const int capacity = 65536;
        byte[] bundle = BundleCases.Pack(File.ReadAllBytes(typeof(Kernels).Assembly.Location),
            File.ReadAllBytes(typeof(Storage).Assembly.Location));
        byte[] output = new byte[capacity];
        int[] location = CompilerExports.CreateDiagnostic();

        Check(nameof(Kernels.Defaults), [], Kernels.Defaults(), []);
        Kernels.Step(5);
        Check(nameof(Kernels.Step), [-2], Kernels.Step(-2), [[5]]);
        foreach (int input in new[] { int.MinValue, -1, 0, 5, short.MaxValue, int.MaxValue })
            Check(nameof(Kernels.Run), [input], Kernels.Run(input), []);
        Check(nameof(Kernels.WithEventArgs), [9], Kernels.WithEventArgs(9), []);
        // Stage 129 (decision 0057): the field read runs the declaring type's initializer.
        Check(nameof(Kernels.RejectInitialized), [], Kernels.RejectInitialized(), []);
        Reject(nameof(Kernels.RejectThreadStatic), Kernels.RejectThreadStatic(), 0);

        void Check(string name, int[] arguments, int expected, int[][] priorArguments)
        {
            byte[] entries = Manifest(Method(name));
            int length = CompilerExports.CompileBundleDetailed(bundle, entries, output, location);
            if (length < 1) throw new InvalidOperationException($"{name} returned {length} at [{string.Join(", ", location)}].");
            string hex = Convert.ToHexString(output.AsSpan(0, length)).ToLowerInvariant();
            cases.Add(new
            {
                module = "BootstrapLibrary", method = "CompileBundle", fixture = "static storage " + name,
                inputs = new[] { Encode(bundle), Encode(entries) },
                generated = new { capacity, bundle = true, arguments, hex, priorArguments },
                expected
            });
        }

        void Reject(string name, int clr, int expectedClr)
        {
            if (clr != expectedClr) throw new InvalidOperationException($"Unexpected CLR result for {name}.");
            MethodInfo method = Method(name);
            byte[] entries = Manifest(method);
            int offset = IlOffsets.Find(method.GetMethodBody()!.GetILAsByteArray()!, OpCodes.Ldsfld);
            int[] diagnostic = [0, Row(method), offset];
            Array.Fill(output, (byte)0xa5);
            int status = CompilerExports.CompileBundleDetailed(bundle, entries, output, location);
            if (offset < 0 || status != -2 || !location.SequenceEqual(diagnostic) || output.Any(value => value != 0xa5))
                throw new InvalidOperationException($"{name} returned {status} at [{string.Join(", ", location)}].");
            cases.Add(new
            {
                module = "BootstrapLibrary", method = "CompileBundle", fixture = "static storage " + name,
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
}
