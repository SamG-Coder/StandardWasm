using System.Buffers.Binary;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using LinkLibrary;

internal static class LinkCases
{
    internal static void Add(List<object> cases)
    {
        const int capacity = 8192;
        byte[] image = File.ReadAllBytes(typeof(LinkKernels).Assembly.Location);
        using var pe = new PEReader(new MemoryStream(image));
        var md = pe.GetMetadataReader();
        var defs = new Dictionary<string, MethodDefinitionHandle>();
        foreach (var handle in md.MethodDefinitions)
        {
            string name = md.GetString(md.GetMethodDefinition(handle).Name);
            if (name == "CombinePaths" || name == "Recur" || name == "Terminal")
                defs.Add(name, handle);
        }

        Verify("CombinePaths", 5, LinkKernels.CombinePaths);
        Verify("Recur", 3, LinkKernels.Recur);

        int root = MetadataTokens.GetRowNumber(defs["CombinePaths"]);
        int rva = md.GetMethodDefinition(defs["CombinePaths"]).RelativeVirtualAddress;
        var section = pe.PEHeaders.SectionHeaders[pe.PEHeaders.GetContainingSectionIndex(rva)];
        int raw = section.PointerToRawData + rva - section.VirtualAddress;
        byte[] il = pe.GetMethodBody(rva).GetILBytes()!;
        int header = (image[raw] & 3) == 2 ? 1 : (image[raw + 1] >> 4) * 4;
        int call = Array.IndexOf(il, (byte)0x28);
        if (call < 0)
            throw new InvalidOperationException("CombinePaths fixture contains no call");
        byte[] unresolved = (byte[])image.Clone();
        BinaryPrimitives.WriteUInt32LittleEndian(unresolved.AsSpan(raw + header + call + 1), 0x06ffffff);
        Reject("unresolved method token", unresolved, -1);

        byte[] inconsistent = (byte[])image.Clone();
        int shared = Enumerable.Range(1, md.GetTableRowCount(TableIndex.MethodDef)).Single(row =>
            md.GetString(md.GetMethodDefinition(MetadataTokens.MethodDefinitionHandle(row)).Name) == "Shared");
        BinaryPrimitives.WriteUInt32LittleEndian(inconsistent.AsSpan(raw + header + call + 1), (uint)(0x06000000 | shared));
        Reject("insufficient arguments for call", inconsistent, -1);

        byte[] initialized = (byte[])image.Clone();
        int strings = pe.PEHeaders.MetadataStartOffset + md.GetHeapMetadataOffset(HeapIndex.String);
        int nameOffset = MetadataTokens.GetHeapOffset(md.GetMethodDefinition(defs["Terminal"]).Name);
        ".cctor\0"u8.CopyTo(initialized.AsSpan(strings + nameOffset));
        Reject("initializer present", initialized, -2);

        void Verify(string fixture, int functions, Func<int, int> oracle)
        {
            int row = MetadataTokens.GetRowNumber(defs[fixture]);
            byte[] output = new byte[capacity];
            int length = LinkDemo.Compile(image, row, output);
            if (length <= 0 || FunctionCount(output.AsSpan(0, length)) != functions)
                throw new InvalidOperationException($"{fixture}: expected {functions} functions, compilation returned length {length}");
            string hex = Convert.ToHexString(output.AsSpan(0, length)).ToLowerInvariant();
            var inputs = new object[] { Encode(image), row };
            foreach (int value in new[] { int.MinValue, -17, -1, 0, 1, 12, 31, int.MaxValue })
                cases.Add(new
                {
                    module = "LinkLibrary", method = "Compile", fixture, inputs,
                    generated = new { capacity, arguments = new[] { value }, hex }, expected = oracle(value)
                });
        }

        void Reject(string fixture, byte[] source, int expected)
        {
            byte[] output = Enumerable.Repeat((byte)0xa5, capacity).ToArray();
            int result = LinkDemo.Compile(source, root, output);
            if (result != expected || output.Any(value => value != 0xa5))
                throw new InvalidOperationException($"{fixture}: CLR compiler returned {result}");
            cases.Add(new
            {
                module = "LinkLibrary", method = "Compile", fixture, inputs = new object[] { Encode(source), root },
                generated = new { capacity, arguments = Array.Empty<int>(), reject = true }, expected
            });
        }
    }

    private static int FunctionCount(ReadOnlySpan<byte> module)
    {
        int cursor = 8;
        while (cursor < module.Length)
        {
            byte section = module[cursor++];
            int size = Uleb(module, ref cursor);
            if (section == 3)
                return Uleb(module, ref cursor);
            cursor += size;
        }
        return 0;
    }

    private static int Uleb(ReadOnlySpan<byte> input, ref int cursor)
    {
        int value = 0, shift = 0;
        while (true)
        {
            int next = input[cursor++];
            value |= (next & 127) << shift;
            if (next < 128)
                return value;
            shift += 7;
        }
    }

    private static object Encode(byte[] bytes) => new { bytes = Array.ConvertAll(bytes, value => (int)value) };
}
