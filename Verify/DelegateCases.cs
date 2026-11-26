using System.Buffers.Binary;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using DelegateLibrary;

internal static class DelegateCases
{
    internal static void Add(List<object> cases)
    {
        const int capacity = 16384;
        byte[] image = File.ReadAllBytes(typeof(Kernels).Assembly.Location);
        int row = typeof(Kernels).GetMethod(nameof(Kernels.Route))!.MetadataToken & 0xffffff;
        var output = new byte[capacity];
        int size = DelegateDemo.Compile(image, row, output);
        if (size <= 0) throw new InvalidOperationException($"Delegate compilation returned {size}.");
        string hex = Convert.ToHexString(output.AsSpan(0, size)).ToLowerInvariant();
        var inputs = new object[] { Encode(image), row };
        foreach (int seed in new[] { int.MinValue, -100, 0, 10, int.MaxValue - 1, int.MaxValue })
        foreach (int branch in new[] { -1, 0, 1 })
        {
            cases.Add(new
            {
                module = "DelegateLibrary", method = "Compile", fixture = "closed callback selection",
                inputs, generated = new { capacity, arguments = new[] { seed, branch }, hex },
                expected = Kernels.Route(seed, branch)
            });
        }

        using var pe = new PEReader(new MemoryStream(image));
        var metadata = pe.GetMetadataReader();
        var handle = MetadataTokens.MethodDefinitionHandle(row);
        int rva = metadata.GetMethodDefinition(handle).RelativeVirtualAddress;
        var section = pe.PEHeaders.SectionHeaders[pe.PEHeaders.GetContainingSectionIndex(rva)];
        int header = section.PointerToRawData + rva - section.VirtualAddress;
        int ilStart = header + ((image[header] & 3) == 2 ? 1 : (image[header + 1] >> 4) * 4);
        byte[] il = pe.GetMethodBody(rva).GetILBytes()!;
        int methodPointer = Enumerable.Range(0, il.Length - 1).First(i => il[i] == 0xfe && il[i + 1] == 6);
        byte[] invalid = (byte[])image.Clone();
        int wrong = typeof(Receiver).GetMethod(nameof(Receiver.OtherArity))!.MetadataToken;
        BinaryPrimitives.WriteInt32LittleEndian(invalid.AsSpan(ilStart + methodPointer + 2), wrong);
        Reject("callback target has an explicit parameter", invalid, row);
        int unsupported = typeof(Kernels).GetMethod(nameof(Kernels.UnsupportedSignature))!.MetadataToken & 0xffffff;
        Reject("unsupported Func<bool> signature", image, unsupported);

        if (FuncIdentityProbe.Probe.Invoke(() => 29) != 29)
            throw new InvalidOperationException("The CLR did not invoke the third-party delegate.");
        var thirdParty = typeof(FuncIdentityProbe.Probe).GetMethod(nameof(FuncIdentityProbe.Probe.Invoke))!;
        Reject("third-party System.Func signature", File.ReadAllBytes(thirdParty.Module.Assembly.Location),
            thirdParty.MetadataToken & 0xffffff);

        void Reject(string fixture, byte[] source, int method)
        {
            Array.Fill(output, (byte)0xa5);
            int actual = DelegateDemo.Compile(source, method, output);
            if (actual != -2 || output.Any(value => value != 0xa5))
                throw new InvalidOperationException($"{fixture} returned {actual} or changed destination.");
            cases.Add(new
            {
                module = "DelegateLibrary", method = "Compile", fixture,
                inputs = new object[] { Encode(source), method },
                generated = new { capacity, arguments = Array.Empty<int>(), reject = true }, expected = -2
            });
        }
    }

    private static object Encode(byte[] bytes) => new { bytes = Array.ConvertAll(bytes, value => (int)value) };
}
