using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using EmitterLibrary;

internal static class EmitterCases
{
    internal static void Add(List<object> cases)
    {
        byte[] image = File.ReadAllBytes(typeof(EmitterDemo).Assembly.Location);
        var encoded = new { bytes = Array.ConvertAll(image, value => (int)value) };
        using var pe = new PEReader(new MemoryStream(image));
        var md = pe.GetMetadataReader();
        var rows = new Dictionary<string, int>();
        foreach (var handle in md.MethodDefinitions)
        {
            string name = md.GetString(md.GetMethodDefinition(handle).Name);
            if (name == "DemoKernel" || name == "Combine" || name == "Mix" || name == "Constant" ||
                name == "Quotient" || name == "UnsupportedSignature" || name == "UnsupportedBranch")
                rows.Add(name, MetadataTokens.GetRowNumber(handle));
        }

        VerifyMethod("DemoKernel", [[11], [-10], [int.MaxValue]], a => EmitterDemo.DemoKernel(a[0]));
        VerifyMethod("Constant", [[]], _ => Kernels.Constant());
        int[] values = [int.MinValue, -42, 0, 15, int.MaxValue];
        int[][] pairs = values.SelectMany(a => values.Select(b => new[] { a, b })).ToArray();
        VerifyMethod("Combine", pairs, a => Kernels.Combine(a[0], a[1]));
        VerifyMethod("Mix", pairs, a => Kernels.Mix(a[0], a[1]));
        VerifyMethod("Quotient", pairs, a => Kernels.Quotient(a[0], a[1]));

        Reject("non-i32 signature", rows["UnsupportedSignature"], 256, -2);
        Reject("control flow", rows["UnsupportedBranch"], 256, -2);
        Reject("insufficient capacity", rows["DemoKernel"], 8, -3);
        Reject("missing row", 0, 256, -1);

        void VerifyMethod(string fixture, int[][] inputs, Func<int[], int> oracle)
        {
            byte[] output = new byte[256];
            int row = rows[fixture];
            int length = EmitterDemo.Compile(image, row, output);
            if (length <= 0)
                throw new InvalidOperationException($"{fixture}: CLR compiler returned {length}");
            string hex = Convert.ToHexString(output.AsSpan(0, length)).ToLowerInvariant();
            foreach (var args in inputs)
            {
                int? expected;
                try { expected = oracle(args); }
                catch (ArithmeticException) { expected = null; }
                cases.Add(new
                {
                    module = "EmitterLibrary", method = "Compile", fixture,
                    inputs = new object[] { encoded, row },
                    generated = new { capacity = output.Length, arguments = args, hex },
                    expected
                });
            }
        }

        void Reject(string fixture, int row, int capacity, int expected)
        {
            var output = new byte[capacity];
            Array.Fill(output, (byte)0xa5);
            if (EmitterDemo.Compile(image, row, output) != expected || output.Any(value => value != 0xa5))
                throw new InvalidOperationException($"CLR compiler did not preserve output for {fixture}");
            cases.Add(new
            {
                module = "EmitterLibrary", method = "Compile", fixture,
                inputs = new object[] { encoded, row },
                generated = new { capacity, arguments = Array.Empty<int>(), reject = true },
                expected
            });
        }
    }
}
