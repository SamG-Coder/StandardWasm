using System.Reflection.Emit;
using AllocatedReceiverDemo;
using BootstrapLibrary;

internal static class AllocatedReceiverCases
{
    internal static void Add(List<object> cases)
    {
        const int capacity = 65536;
        byte[] bundle = BundleCases.Pack(File.ReadAllBytes(typeof(Kernels).Assembly.Location));
        object encodedBundle = Encode(bundle);
        byte[] output = new byte[capacity];
        int[] location = CompilerExports.CreateDiagnostic();

        Check(nameof(Kernels.Direct), Kernels.Direct);
        Check(nameof(Kernels.Initializer), Kernels.Initializer);
        Check(nameof(Kernels.SameClassJoin), Kernels.SameClassJoin);

        if (Kernels.Unknown(new Leaf(5)) != 45 || Kernels.MixedJoin(new Leaf(5), -1) != 45 ||
            Kernels.MixedJoin(new Leaf(5), 1) != 21 ||
            Kernels.ReplacementCall(5) != 45 || Kernels.ReplacementField(5) != 45 ||
            Kernels.ReplacementElement(5) != 45)
            throw new InvalidOperationException("CLR virtual dispatch did not reach the expected override.");

        Reject(nameof(Kernels.Unknown));
        Reject(nameof(Kernels.MixedJoin));
        Reject(nameof(Kernels.ReplacementCall));
        Reject(nameof(Kernels.ReplacementField));
        Reject(nameof(Kernels.ReplacementElement));

        // Confirm the compiler did not fold the source fixtures into a
        // single allocation or erase the duplicated receiver.
        if (Find(nameof(Kernels.SameClassJoin), OpCodes.Newobj, 1) < 0 ||
            Find(nameof(Kernels.Initializer), OpCodes.Dup, 0) < 0)
            throw new InvalidOperationException("The fixtures no longer contain the intended CLI stack shapes.");

        void Check(string name, Func<int, int> oracle)
        {
            byte[] entries = BundleCases.Manifest(typeof(Kernels), name);
            int length = CompilerExports.CompileBundleDetailed(bundle, entries, output, location);
            if (length < 1)
                throw new InvalidOperationException($"{name} returned {length} at [{string.Join(", ", location)}].");
            string hex = Convert.ToHexString(output.AsSpan(0, length)).ToLowerInvariant();
            object[] inputs = [encodedBundle, Encode(entries)];
            foreach (int input in new[] { int.MinValue, -1, 0, 5, int.MaxValue })
                cases.Add(new
                {
                    module = "BootstrapLibrary", method = "CompileBundle", fixture = "allocated receiver " + name,
                    inputs, generated = new { capacity, bundle = true, arguments = new[] { input }, hex },
                    expected = oracle(input)
                });
        }

        void Reject(string name)
        {
            byte[] entries = BundleCases.Manifest(typeof(Kernels), name);
            int row = typeof(Kernels).GetMethod(name)!.MetadataToken & 0xffffff;
            int call = Find(name, OpCodes.Callvirt, 0);
            int[] diagnostic = [0, row, call];
            Array.Fill(output, (byte)0xa5);
            int status = CompilerExports.CompileBundleDetailed(bundle, entries, output, location);
            if (call < 0 || status != -2 || !location.SequenceEqual(diagnostic) || output.Any(value => value != 0xa5))
                throw new InvalidOperationException($"{name} returned {status} at [{string.Join(", ", location)}], expected -2 at [{string.Join(", ", diagnostic)}].");
            cases.Add(new
            {
                module = "BootstrapLibrary", method = "CompileBundle", fixture = "unknown receiver " + name,
                inputs = new[] { encodedBundle, Encode(entries) },
                generated = new { capacity, bundle = true, arguments = Array.Empty<int>(), reject = true, diagnostic },
                expected = -2
            });
        }
    }

    private static int Find(string name, OpCode opcode, int occurrence) =>
        IlOffsets.Find(typeof(Kernels).GetMethod(name)!.GetMethodBody()!.GetILAsByteArray()!, opcode, occurrence);

    private static object Encode(byte[] bytes) => new { bytes = Array.ConvertAll(bytes, value => (int)value) };
}
