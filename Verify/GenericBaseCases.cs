using System.Reflection.Emit;
using BootstrapLibrary;
using GenericBaseDemo;
using GenericBaseDependency;

internal static class GenericBaseCases
{
    internal static void Add(List<object> cases)
    {
        const int capacity = 65536;
        byte[] bundle = BundleCases.Pack(File.ReadAllBytes(typeof(Kernels).Assembly.Location),
            File.ReadAllBytes(typeof(Root).Assembly.Location));
        object encoded = Encode(bundle);
        byte[] output = new byte[capacity];
        int[] location = CompilerExports.CreateDiagnostic();

        Check(nameof(Kernels.Local), Kernels.Local);
        Check(nameof(Kernels.Linked), Kernels.Linked);
        Check(nameof(Kernels.ReusedAndDistinct), Kernels.ReusedAndDistinct);
        Check(nameof(Kernels.RootVirtual), Kernels.RootVirtual);
        Check(nameof(Kernels.NestedArgument), Kernels.NestedArgument);

        // This valid CLR shape needs its own class identity as a type argument
        // before its base can be reserved. Keep that cycle diagnosed.
        if (Kernels.RecursiveTypeArgument() != 1) throw new InvalidOperationException("CLR recursive type fixture drifted.");
        var unsupported = typeof(Kernels).GetMethod(nameof(Kernels.RecursiveTypeArgument))!;
        int offset = IlOffsets.Find(unsupported.GetMethodBody()!.GetILAsByteArray()!, OpCodes.Newobj);
        int[] expectedLocation = [0, unsupported.MetadataToken & 0xffffff, offset];
        byte[] entries = BundleCases.Manifest(typeof(Kernels), unsupported.Name);
        Array.Fill(output, (byte)0xa5);
        int status = CompilerExports.CompileBundleDetailed(bundle, entries, output, location);
        if (offset < 0 || status != -2 || !location.SequenceEqual(expectedLocation) || output.Any(value => value != 0xa5))
            throw new InvalidOperationException($"Recursive base returned {status} at [{string.Join(", ", location)}].");
        cases.Add(new
        {
            module = "BootstrapLibrary", method = "CompileBundle", fixture = "generic base " + unsupported.Name,
            inputs = new[] { encoded, Encode(entries) },
            generated = new { capacity, bundle = true, arguments = Array.Empty<int>(), reject = true, diagnostic = expectedLocation },
            expected = status
        });

        void Check(string name, Func<int, int> oracle)
        {
            byte[] selection = BundleCases.Manifest(typeof(Kernels), name);
            int length = CompilerExports.CompileBundleDetailed(bundle, selection, output, location);
            if (length < 1)
                throw new InvalidOperationException($"{name} returned {length} at [{string.Join(", ", location)}].");
            string hex = Convert.ToHexString(output.AsSpan(0, length)).ToLowerInvariant();
            foreach (int value in new[] { int.MinValue, -1, 0, 5, int.MaxValue })
                cases.Add(new
                {
                    module = "BootstrapLibrary", method = "CompileBundle", fixture = "generic base " + name,
                    inputs = new[] { encoded, Encode(selection) },
                    generated = new { capacity, bundle = true, arguments = new[] { value }, hex },
                    expected = oracle(value)
                });
        }
    }

    private static object Encode(byte[] bytes) => new { bytes = Array.ConvertAll(bytes, value => (int)value) };
}
