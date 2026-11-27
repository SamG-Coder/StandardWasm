using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using TypedArrayLibrary;

internal static class TypedArrayCases
{
    internal static void Add(List<object> cases)
    {
        const int capacity = 16384;
        byte[] image = File.ReadAllBytes(typeof(ArrayKernels).Assembly.Location);
        using var pe = new PEReader(new MemoryStream(image));
        var md = pe.GetMetadataReader();
        var handle = md.MethodDefinitions.Single(method =>
            md.GetString(md.GetMethodDefinition(method).Name) == "Route");
        int row = MetadataTokens.GetRowNumber(handle);
        byte[] output = new byte[capacity];
        int length = ArrayDemo.Compile(image, row, output);
        if (length <= 0)
            throw new InvalidOperationException($"Route compilation returned {length}");
        var inputs = new object[] { Encode(image), row };
        string hex = Convert.ToHexString(output.AsSpan(0, length)).ToLowerInvariant();
        foreach (int value in new[] { int.MinValue, -1, 0, 127, 128, 255, 256, 511, int.MaxValue })
        foreach (int count in new[] { -1, 0, 1, 3 })
        {
            int? expected;
            string exceptionType = null;
            try { expected = ArrayKernels.Route(value, count); }
            catch (ArithmeticException error) { expected = null; exceptionType = error.GetType().FullName; }
            catch (IndexOutOfRangeException error) { expected = null; exceptionType = error.GetType().FullName; }
            cases.Add(new
            {
                module = "TypedArrayLibrary", method = "Compile", fixture = "Route", inputs,
                generated = new { capacity, arguments = new[] { value, count }, hex, exceptionType }, expected
            });
        }

        int rva = md.GetMethodDefinition(handle).RelativeVirtualAddress;
        var section = pe.PEHeaders.SectionHeaders[pe.PEHeaders.GetContainingSectionIndex(rva)];
        int start = section.PointerToRawData + rva - section.VirtualAddress;
        int header = (image[start + 1] >> 4) * 4;
        byte[] il = pe.GetMethodBody(rva).GetILBytes()!;
        // Decode instructions to find the newarr opcode. A raw byte search can hit 0x8d inside
        // another instruction's token, which shifts whenever the library gains a method.
        int allocate = IlOffsets.Find(il, OpCodes.Newarr);
        if (header < 12 || allocate < 0)
            throw new InvalidOperationException("Route did not use the expected fat body and inline int[] allocation");
        byte[] wrongLocal = (byte[])image.Clone();
        byte[] wrong = [0x17, .. il.AsSpan(allocate, 5), 0x0a, 0x16, 0x2a];
        System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(wrongLocal.AsSpan(start + 4), wrong.Length);
        wrong.CopyTo(wrongLocal, start + header);
        Array.Fill(output, (byte)0xa5);
        if (ArrayDemo.Compile(wrongLocal, row, output) != -1 || output.Any(value => value != 0xa5))
            throw new InvalidOperationException("Compiler accepted an int[] store into a byte[] local");
        cases.Add(new
        {
            module = "TypedArrayLibrary", method = "Compile", fixture = "incompatible reference stored to byte[] local",
            inputs = new object[] { Encode(wrongLocal), row },
            generated = new { capacity, arguments = Array.Empty<int>(), reject = true }, expected = -1
        });

    }

    private static object Encode(byte[] bytes) => new { bytes = Array.ConvertAll(bytes, value => (int)value) };
}
