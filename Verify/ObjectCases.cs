using System.Buffers.Binary;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using ObjectLibrary;

internal static class ObjectCases
{
    internal static void Add(List<object> cases)
    {
        const int capacity = 16384;
        byte[] image = File.ReadAllBytes(typeof(Kernels).Assembly.Location);
        using var pe = new PEReader(new MemoryStream(image));
        var metadata = pe.GetMetadataReader();
        var type = typeof(Kernels);
        int isolated = type.GetMethod(nameof(Kernels.Isolated))!.MetadataToken & 0xffffff;
        int read = typeof(Counter).GetMethod(nameof(Counter.Read))!.MetadataToken & 0xffffff;
        int field = typeof(Other).GetField(nameof(Other.Value))!.MetadataToken;
        int wrongMethod = typeof(Other).GetMethod(nameof(Other.Read))!.MetadataToken;

        var output = new byte[capacity];
        int size = ObjectDemo.Compile(image, isolated, output);
        if (size <= 0) throw new InvalidOperationException($"Object compilation returned {size}.");
        string hex = Convert.ToHexString(output.AsSpan(0, size)).ToLowerInvariant();
        var inputs = new object[] { Encode(image), isolated };
        foreach (int seed in new[] { int.MinValue, -100, 0, 10, int.MaxValue - 1, int.MaxValue })
        {
            cases.Add(new
            {
                module = "ObjectLibrary", method = "Compile", fixture = "independent object fields",
                inputs, generated = new { capacity, arguments = new[] { seed }, hex }, expected = Kernels.Isolated(seed)
            });
        }

        AddThirdPartyBaseCase(cases, output);

        Reject("wrong field owner", read, OpCodes.Ldfld, field);
        Reject("wrong direct call signature", isolated, OpCodes.Callvirt, wrongMethod);

        void Reject(string fixture, int row, OpCode operation, int token)
        {
            var definition = metadata.GetMethodDefinition(MetadataTokens.MethodDefinitionHandle(row));
            int rva = definition.RelativeVirtualAddress;
            var section = pe.PEHeaders.SectionHeaders[pe.PEHeaders.GetContainingSectionIndex(rva)];
            int header = section.PointerToRawData + rva - section.VirtualAddress;
            int ilStart = header + ((image[header] & 3) == 2 ? 1 : (image[header + 1] >> 4) * 4);
            byte[] il = pe.GetMethodBody(rva).GetILBytes()!;
            int at = IlOffsets.Find(il, operation);
            if (at < 0) throw new InvalidOperationException($"Fixture lacks opcode {operation.Name}.");
            var invalid = (byte[])image.Clone();
            BinaryPrimitives.WriteInt32LittleEndian(invalid.AsSpan(ilStart + at + 1), token);
            Array.Fill(output, (byte)0xa5);
            int status = ObjectDemo.Compile(invalid, row, output);
            if (status != -1 || output.Any(x => x != 0xa5))
                throw new InvalidOperationException("Rejected object method did not preserve the output buffer.");
            cases.Add(new
            {
                module = "ObjectLibrary", method = "Compile", fixture,
                inputs = new object[] { Encode(invalid), row },
                generated = new { capacity, arguments = Array.Empty<int>(), reject = true }, expected = status
            });
        }
    }

    private static void AddThirdPartyBaseCase(List<object> cases, byte[] output)
    {
        try
        {
            ObjectIdentityProbe.Probe.Make(7);
            throw new InvalidOperationException("Expected the third-party base constructor to run.");
        }
        catch (InvalidOperationException error) when (error.Message == "The third-party base constructor ran.")
        {
        }

        var method = typeof(ObjectIdentityProbe.Probe).GetMethod(nameof(ObjectIdentityProbe.Probe.Make))!;
        int row = method.MetadataToken & 0xffffff;
        byte[] image = File.ReadAllBytes(method.Module.Assembly.Location);
        Array.Fill(output, (byte)0xa5);
        int status = ObjectDemo.Compile(image, row, output);
        if (status != -2 || output.Any(value => value != 0xa5))
            throw new InvalidOperationException($"Third-party System.Object must be rejected before emission. Status: {status}.");
        cases.Add(new
        {
            module = "ObjectLibrary", method = "Compile", fixture = "third-party System.Object base",
            inputs = new object[] { Encode(image), row },
            generated = new { capacity = output.Length, arguments = Array.Empty<int>(), reject = true },
            expected = status
        });
    }

    private static object Encode(byte[] bytes) => new { bytes = Array.ConvertAll(bytes, value => (int)value) };
}
