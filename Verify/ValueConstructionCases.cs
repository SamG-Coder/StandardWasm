using System.Buffers.Binary;
using System.Reflection;
using System.Reflection.Emit;
using BootstrapLibrary;
using ValueConstructionDemo;
using ValueConstructionDependency;

// Stage 111: newobj and in-place construction of local and linked value types.
internal static class ValueConstructionCases
{
    internal static void Add(List<object> cases)
    {
        const int capacity = 65536;
        byte[] bundle = BundleCases.Pack(File.ReadAllBytes(typeof(Kernels).Assembly.Location),
            File.ReadAllBytes(typeof(Extent).Assembly.Location));
        byte[] output = new byte[capacity];
        int[] location = CompilerExports.CreateDiagnostic();
        Check(nameof(Kernels.Run), Kernels.Run);
        Check(nameof(Kernels.Defaults), Kernels.Defaults);
        Check(nameof(Kernels.Fields), Kernels.Fields);
        Check(nameof(Kernels.Copies), Kernels.Copies);
        Check(nameof(Kernels.Chained), Kernels.Chained);
        Check(nameof(Kernels.Argument), Kernels.Argument);
        Check(nameof(Kernels.Guarded), Kernels.Guarded);

        // A receiver method call and a byref argument need general managed byrefs.
        ConstructorInfo rejected = typeof(Rejected).GetConstructor([typeof(int)])!;
        Reject(Method(nameof(Kernels.RejectReceiverCall)), [0, Row(rejected), Offset(rejected, OpCodes.Call)]);
        MethodInfo borrow = Method(nameof(Kernels.RejectBorrow));
        Reject(borrow, [0, Row(borrow), Offset(borrow, OpCodes.Call, 1)]);

        void Check(string name, Func<int, int> expectedResult)
        {
            MethodInfo run = Method(name);
            byte[] entries = Manifest(run);
            int length = CompilerExports.CompileBundleDetailed(bundle, entries, output, location);
            if (length < 1) throw new InvalidOperationException($"Value construction {name} returned {length} at [{string.Join(", ", location)}].");
            string hex = Convert.ToHexString(output.AsSpan(0, length)).ToLowerInvariant();
            foreach (int input in new[] { int.MinValue, -5, 0, 5, int.MaxValue })
                cases.Add(new
                {
                    module = "BootstrapLibrary", method = "CompileBundle", fixture = "value construction " + name,
                    inputs = new[] { Encode(bundle), Encode(entries) },
                    generated = new { capacity, bundle = true, arguments = new[] { input }, hex },
                    expected = expectedResult(input)
                });
        }

        void Reject(MethodBase method, int[] diagnostic)
        {
            byte[] entries = Manifest(method);
            Array.Fill(output, (byte)0xa5);
            int status = CompilerExports.CompileBundleDetailed(bundle, entries, output, location);
            if (status != -2 || !location.SequenceEqual(diagnostic) || output.Any(value => value != 0xa5))
                throw new InvalidOperationException($"{method.Name} returned {status} at [{string.Join(", ", location)}].");
            cases.Add(new
            {
                module = "BootstrapLibrary", method = "CompileBundle", fixture = "value construction " + method.Name,
                inputs = new[] { Encode(bundle), Encode(entries) },
                generated = new { capacity, bundle = true, arguments = Array.Empty<int>(), reject = true, diagnostic },
                expected = status
            });
        }
    }

    private static MethodInfo Method(string name) => typeof(Kernels).GetMethod(name)!;
    private static int Row(MethodBase method) => method.MetadataToken & 0xffffff;

    private static int Offset(MethodBase method, OpCode opcode, int skip = 0)
    {
        int offset = IlOffsets.Find(method.GetMethodBody()!.GetILAsByteArray()!, opcode, skip);
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
