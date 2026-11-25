using System.Buffers.Binary;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using FlowLibrary;

internal static class FlowCases
{
    internal static void Add(List<object> cases)
    {
        const int capacity = 4096;
        byte[] image = File.ReadAllBytes(typeof(FlowKernels).Assembly.Location);
        using var pe = new PEReader(new MemoryStream(image));
        var md = pe.GetMetadataReader();
        var defs = new Dictionary<string, MethodDefinitionHandle>();
        foreach (var handle in md.MethodDefinitions)
        {
            string name = md.GetString(md.GetMethodDefinition(handle).Name);
            if (name == "Fold" || name == "Select" || name == "UnsignedChoice")
                defs.Add(name, handle);
        }

        Verify("Fold", [[0, 0], [-1, 7], [1, 0], [6, 20], [511, -7], [200, int.MinValue], [2, int.MaxValue]],
            x => FlowKernels.Fold(x[0], x[1]));
        Verify("Select", [[8, 4, 5], [8, 5, 4], [0, 0, 0], [int.MaxValue, -15, 2], [int.MinValue, 15, -2]],
            x => FlowKernels.Select(x[0], x[1], x[2]));
        Verify("UnsignedChoice", [[-1, 0], [0, -1], [int.MinValue, 9], [9, int.MinValue], [0, 0]],
            x => FlowKernels.UnsignedChoice(x[0], x[1]));

        var fold = defs["Fold"];
        int rva = md.GetMethodDefinition(fold).RelativeVirtualAddress;
        var section = pe.PEHeaders.SectionHeaders[pe.PEHeaders.GetContainingSectionIndex(rva)];
        int start = section.PointerToRawData + rva - section.VirtualAddress;
        int header = (image[start + 1] >> 4) * 4;
        if ((image[start] & 3) != 3 || pe.GetMethodBody(rva).LocalSignature.IsNil)
            throw new InvalidOperationException("Fold must have a fat header with local slots");
        Reject("branch into an operand", [0x2b, 0x01, 0x1f, 0x00, 0x2a]);
        Reject("invalid local index", [0x11, 0xff, 0x2a]);
        Reject("inconsistent stack merge", [0x02, 0x2d, 0x01, 0x16, 0x17, 0x2a]);
        Reject("fallthrough beyond method", [0x16]);
        Reject("stack underflow", [0x58, 0x2a]);

        void Verify(string name, int[][] arguments, Func<int[], int> oracle)
        {
            int row = MetadataTokens.GetRowNumber(defs[name]);
            byte[] output = new byte[capacity];
            int length = FlowDemo.Compile(image, row, output);
            if (length <= 0)
                throw new InvalidOperationException($"{name}: CLR compiler returned {length}");
            var inputs = new object[] { Encode(image), row };
            string hex = Convert.ToHexString(output.AsSpan(0, length)).ToLowerInvariant();
            foreach (int[] args in arguments)
                cases.Add(new
                {
                    module = "FlowLibrary", method = "Compile", fixture = name, inputs,
                    generated = new { capacity, arguments = args, hex }, expected = oracle(args)
                });
        }

        void Reject(string name, byte[] il)
        {
            byte[] malformed = (byte[])image.Clone();
            BinaryPrimitives.WriteInt32LittleEndian(malformed.AsSpan(start + 4), il.Length);
            il.CopyTo(malformed, start + header);
            int row = MetadataTokens.GetRowNumber(fold);
            byte[] output = Enumerable.Repeat((byte)0xa5, capacity).ToArray();
            int result = FlowDemo.Compile(malformed, row, output);
            if (result != -1 || output.Any(value => value != 0xa5))
                throw new InvalidOperationException($"{name}: expected diagnostic -1, CLR returned {result}");
            cases.Add(new
            {
                module = "FlowLibrary", method = "Compile", fixture = name,
                inputs = new object[] { Encode(malformed), row },
                generated = new { capacity, arguments = Array.Empty<int>(), reject = true }, expected = -1
            });
        }
    }

    private static object Encode(byte[] bytes) => new { bytes = Array.ConvertAll(bytes, value => (int)value) };
}
