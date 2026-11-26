using System.Buffers.Binary;
using System.Reflection.Emit;
using BootstrapLibrary;
using GenericBodyConsumer;
using GenericBodyDefinition;
using GenericBodyDemo;

// Stage 105: specialized bodies on closed generic declaring types (#193).
internal static class GenericBodyCases
{
    internal static void Add(List<object> cases)
    {
        const int capacity = 65536;
        byte[] app = File.ReadAllBytes(typeof(Kernels).Assembly.Location);
        byte[] definition = File.ReadAllBytes(typeof(Holder<>).Assembly.Location);
        byte[] consumer = File.ReadAllBytes(typeof(Consumer).Assembly.Location);
        byte[] bundle = BundleCases.Pack(app, definition, consumer);
        byte[] output = new byte[capacity];
        int[] location = CompilerExports.CreateDiagnostic();
        var compiled = new Dictionary<string, (object[] Inputs, string Hex)>();

        int[] edges = [int.MinValue, int.MinValue + 1, -129, -128, -2, -1, 0, 1, 2, 127, 128, 255, 256, 0x7fff, int.MaxValue];
        foreach (int value in edges)
        {
            Check(nameof(Kernels.Run), [value], () => Kernels.Run(value));
            Check(nameof(Kernels.Narrow), [value], () => Kernels.Narrow(value));
            Check(nameof(Kernels.Lifecycle), [value], () => Kernels.Lifecycle(value));
            Check(nameof(Kernels.Shared), [value], () => Kernels.Shared(value));
            Check(nameof(Kernels.Text), [value], () => Kernels.Text(value));
        }
        int[] operands = [int.MinValue, -7, -2, -1, 0, 1, 2, 7, int.MaxValue];
        foreach (int left in operands)
            foreach (int right in operands)
            {
                Check(nameof(Kernels.SignedQuotient), [left, right], () => Kernels.SignedQuotient(left, right));
                Check(nameof(Kernels.UnsignedQuotient), [left, right], () => Kernels.UnsignedQuotient(left, right));
                Check(nameof(Kernels.Compare), [left, right], () => Kernels.Compare(left, right));
            }

        // Generic method parameters and virtual dispatch stay separately tracked.
        if (Kernels.GenericMethod(3) != 3 || Kernels.Virtual(3) != 1)
            throw new InvalidOperationException("The CLR generic body rejection fixtures did not execute.");
        Reject("generic method", nameof(Kernels.GenericMethod), OpCodes.Call, -2);
        Reject("virtual member", nameof(Kernels.Virtual), OpCodes.Callvirt, -2);

        void Check(string name, int[] arguments, Func<int> clr)
        {
            if (!compiled.TryGetValue(name, out var module))
            {
                byte[] entries = Manifest(name);
                int size = CompilerExports.CompileBundleDetailed(bundle, entries, output, location);
                if (size <= 0)
                    throw new InvalidOperationException($"{name} returned {size} at [{string.Join(", ", location)}].");
                module = ([Encode(bundle), Encode(entries)], Convert.ToHexString(output.AsSpan(0, size)).ToLowerInvariant());
                compiled.Add(name, module);
            }
            int? expected = null;
            string exception = null;
            try { expected = clr(); }
            catch (Exception error) when (error is NullReferenceException or DivideByZeroException or OverflowException)
            {
                exception = error.GetType().FullName;
            }
            cases.Add(new
            {
                module = "BootstrapLibrary", method = "CompileBundle", fixture = "generic body " + name,
                inputs = module.Inputs, generated = new { capacity, bundle = true, arguments, hex = module.Hex, exceptionType = exception },
                expected, exception
            });
        }

        void Reject(string label, string name, OpCode opcode, int expected)
        {
            byte[] entries = Manifest(name);
            int[] diagnostic = [0, Row(name), Offset(name, opcode)];
            Array.Fill(output, (byte)0xa5);
            int status = CompilerExports.CompileBundleDetailed(bundle, entries, output, location);
            if (status != expected || !location.SequenceEqual(diagnostic) || output.Any(b => b != 0xa5))
                throw new InvalidOperationException($"{label} returned {status} at [{string.Join(", ", location)}].");
            cases.Add(new
            {
                module = "BootstrapLibrary", method = "CompileBundle", fixture = "generic body " + label,
                inputs = new[] { Encode(bundle), Encode(entries) },
                generated = new { capacity, bundle = true, arguments = Array.Empty<int>(), reject = true, diagnostic },
                expected = status
            });
        }
    }

    private static int Row(string name) => typeof(Kernels).GetMethod(name)!.MetadataToken & 0xffffff;

    private static int Offset(string name, OpCode opcode)
    {
        byte[] il = typeof(Kernels).GetMethod(name)!.GetMethodBody()!.GetILAsByteArray()!;
        int offset = IlOffsets.Find(il, opcode);
        return offset >= 0 ? offset : throw new InvalidOperationException($"{name} did not emit {opcode}.");
    }

    private static byte[] Manifest(string name)
    {
        byte[] entries = new byte[11];
        BinaryPrimitives.WriteInt32LittleEndian(entries, Row(name));
        entries[4] = 3;
        "run"u8.CopyTo(entries.AsSpan(8));
        return entries;
    }

    private static object Encode(byte[] bytes) => new { bytes = Array.ConvertAll(bytes, value => (int)value) };
}
