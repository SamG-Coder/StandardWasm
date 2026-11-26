using System.Buffers.Binary;
using System.Reflection;
using System.Reflection.Emit;
using BootstrapLibrary;
using BeforeFieldInitDemo;
using BeforeFieldInitDependency;

internal static class BeforeFieldInitCases
{
    internal static void Add(List<object> cases)
    {
        const int capacity = 65536;
        byte[] bundle = BundleCases.Pack(File.ReadAllBytes(typeof(Kernels).Assembly.Location),
            File.ReadAllBytes(typeof(Relaxed).Assembly.Location));
        byte[] output = new byte[capacity];
        int[] location = CompilerExports.CreateDiagnostic();
        Check(nameof(Kernels.Run), Kernels.Run);
        Check(nameof(Kernels.Construct), Kernels.Construct);

        if (Kernels.NeedsField() != 42 || Journal.Value != 42 || Kernels.NeedsPrecise(5) != 78 || Journal.Value != 73)
            throw new InvalidOperationException("Unexpected CLR initialization trigger.");
        MethodInfo field = typeof(Relaxed).GetMethod(nameof(Relaxed.Read))!;
        Reject(Method(nameof(Kernels.NeedsField)), [1, Row(field), Offset(field, OpCodes.Ldsfld)]);
        MethodInfo precise = Method(nameof(Kernels.NeedsPrecise));
        Reject(precise, [0, Row(precise), Offset(precise, OpCodes.Call)]);
        ConstructorInfo initializer = typeof(Local).TypeInitializer!;
        Reject(initializer, [0, Row(initializer), -1]);

        void Check(string name, Func<int, int> expectedResult)
        {
            MethodInfo run = Method(name);
            byte[] entries = Manifest(run);
            int length = CompilerExports.CompileBundleDetailed(bundle, entries, output, location);
            if (length < 1) throw new InvalidOperationException($"Relaxed calls returned {length} at [{string.Join(", ", location)}].");
            string hex = Convert.ToHexString(output.AsSpan(0, length)).ToLowerInvariant();
            foreach (int input in new[] { int.MinValue, -5, 0, 5, int.MaxValue })
            {
                int expected = expectedResult(input);
                if (Journal.Value != 0) throw new InvalidOperationException("The CLR ran a relaxed initializer before the selected fixture boundary.");
                cases.Add(new
                {
                    module = "BootstrapLibrary", method = "CompileBundle", fixture = "beforefieldinit " + name,
                    inputs = new[] { Encode(bundle), Encode(entries) },
                    generated = new { capacity, bundle = true, arguments = new[] { input }, hex },
                    expected
                });
            }
        }

        void Reject(MethodBase method, int[] diagnostic)
        {
            byte[] rejected = Manifest(method);
            Array.Fill(output, (byte)0xa5);
            int status = CompilerExports.CompileBundleDetailed(bundle, rejected, output, location);
            if (status != -2 || !location.SequenceEqual(diagnostic) || output.Any(value => value != 0xa5))
                throw new InvalidOperationException($"{method.Name} returned {status} at [{string.Join(", ", location)}].");
            cases.Add(new
            {
                module = "BootstrapLibrary", method = "CompileBundle", fixture = "beforefieldinit " + method.Name,
                inputs = new[] { Encode(bundle), Encode(rejected) },
                generated = new { capacity, bundle = true, arguments = Array.Empty<int>(), reject = true, diagnostic },
                expected = status
            });
        }
    }

    private static MethodInfo Method(string name) => typeof(Kernels).GetMethod(name)!;
    private static int Row(MethodBase method) => method.MetadataToken & 0xffffff;

    private static int Offset(MethodInfo method, OpCode opcode)
    {
        int offset = IlOffsets.Find(method.GetMethodBody()!.GetILAsByteArray()!, opcode);
        if (offset < 0) throw new InvalidOperationException($"{method.Name} lacks {opcode}.");
        return offset;
    }

    private static byte[] Manifest(MethodBase method)
    {
        byte[] entries = new byte[11];
        BinaryPrimitives.WriteInt32LittleEndian(entries, Row(method));
        entries[4] = 3;
        "run"u8.CopyTo(entries.AsSpan(8));
        return entries;
    }

    private static object Encode(byte[] bytes) => new { bytes = Array.ConvertAll(bytes, value => (int)value) };
}
