using System.Buffers.Binary;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using BootstrapLibrary;
using WideLayoutDemo;
using WideLayoutDependency;

internal static class WideLayoutCases
{
    internal static void Add(List<object> cases)
    {
        const int capacity = 65536, tooSmall = 256;
        var entry = typeof(Kernels).GetMethod(nameof(Kernels.Run))!;
        if (typeof(LayoutTable).GetFields().Length != 311)
            throw new InvalidOperationException("Unexpected reachable type fixture size.");
        byte[] image = File.ReadAllBytes(entry.DeclaringType!.Assembly.Location);
        byte[] dependency = File.ReadAllBytes(typeof(LayoutTable).Assembly.Location);
        byte[] bundle = BundleCases.Pack(image, dependency);
        byte[] manifest = BootstrapCases.Manifest([("run", entry.MetadataToken & 0xffffff)]);
        object[] inputs = [Encode(bundle), Encode(manifest)];
        byte[] output = new byte[capacity];
        int[] location = CompilerExports.CreateDiagnostic();
        int size = CompilerExports.CompileBundleDetailed(bundle, manifest, output, location);
        if (size <= 0)
            throw new InvalidOperationException($"Wide layout compilation returned {size} at [{string.Join(", ", location)}].");
        string hex = Convert.ToHexString(output.AsSpan(0, size)).ToLowerInvariant();
        foreach (int value in new[] { int.MinValue, -17, 0, 5, int.MaxValue })
            foreach (int mode in new[] { 0, 1 })
                cases.Add(new
                {
                    module = "BootstrapLibrary", method = "CompileBundle", fixture = "wide managed layouts",
                    inputs, generated = new { capacity, bundle = true, arguments = new[] { value, mode }, hex },
                    expected = Kernels.Run(value, mode)
                });

        byte[] small = Enumerable.Repeat((byte)0xa5, tooSmall).ToArray();
        if (CompilerExports.CompileBundle(bundle, manifest, small) != -3 || small.Any(value => value != 0xa5))
            throw new InvalidOperationException("The small output failed to preserve the original buffer.");
        cases.Add(new
        {
            module = "BootstrapLibrary", method = "CompileBundle", fixture = "wide layouts output capacity",
            inputs, generated = new { capacity = tooSmall, bundle = true, arguments = Array.Empty<int>(), reject = true },
            expected = -3
        });

        // CLI maxstack is an upper bound. Increasing it keeps the program's
        // behavior while exercising the local count around the engine limit.
        int row = entry.MetadataToken & 0xffffff;
        byte[] below = WithMaxStack(image, dependency, row, 149);
        int length = CompilerExports.CompileBundleDetailed(below, manifest, output, location);
        if (length <= 0)
            throw new InvalidOperationException($"Wide layout near local limit returned {length} at [{string.Join(", ", location)}].");
        cases.Add(new
        {
            module = "BootstrapLibrary", method = "CompileBundle", fixture = "wide layouts below local limit",
            inputs = new[] { Encode(below), Encode(manifest) },
            generated = new { capacity, bundle = true, arguments = new[] { 5, 0 },
                hex = Convert.ToHexString(output.AsSpan(0, length)).ToLowerInvariant() },
            expected = Kernels.Run(5, 0)
        });

        byte[] above = WithMaxStack(image, dependency, row, 150);
        int[] diagnostic = [0, row, -1];
        Array.Fill(output, (byte)0xa5);
        int status = CompilerExports.CompileBundleDetailed(above, manifest, output, location);
        if (status != -2 || !location.SequenceEqual(diagnostic) || output.Any(value => value != 0xa5))
            throw new InvalidOperationException($"Wide layout local limit returned {status} at [{string.Join(", ", location)}].");
        cases.Add(new
        {
            module = "BootstrapLibrary", method = "CompileBundle", fixture = "wide layouts above local limit",
            inputs = new[] { Encode(above), Encode(manifest) },
            generated = new { capacity, bundle = true, arguments = Array.Empty<int>(), reject = true, diagnostic },
            expected = status
        });
    }

    private static byte[] WithMaxStack(byte[] image, byte[] dependency, int row, ushort maximum)
    {
        using var pe = new PEReader(new MemoryStream(image));
        int rva = pe.GetMetadataReader().GetMethodDefinition(MetadataTokens.MethodDefinitionHandle(row)).RelativeVirtualAddress;
        var section = pe.PEHeaders.SectionHeaders[pe.PEHeaders.GetContainingSectionIndex(rva)];
        int offset = section.PointerToRawData + rva - section.VirtualAddress;
        if ((image[offset] & 3) != 3) throw new InvalidOperationException("Expected a fat method header.");
        byte[] clone = (byte[])image.Clone();
        BinaryPrimitives.WriteUInt16LittleEndian(clone.AsSpan(offset + 2), maximum);
        return BundleCases.Pack(clone, dependency);
    }

    private static object Encode(byte[] bytes) => new { bytes = Array.ConvertAll(bytes, value => (int)value) };
}
