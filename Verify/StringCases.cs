using System.Buffers.Binary;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using StringLibrary;

internal static class StringCases
{
    internal static void Add(List<object> cases)
    {
        const int capacity = 16384;
        byte[] image = File.ReadAllBytes(typeof(Kernels).Assembly.Location);
        var output = new byte[capacity];

        Verify(nameof(Kernels.Length), Enumerable.Range(0, 4).Select(choice => new[] { choice }), args => Kernels.Length(args[0]));
        var indexes = new List<int[]>();
        for (int choice = 0; choice < 4; choice++)
        {
            int length = Kernels.Length(choice);
            foreach (int i in new[] { -1, 0, 1, 2, 3, 4, 63, 64, length - 1, length }.Distinct())
                indexes.Add([choice, i]);
        }
        Verify(nameof(Kernels.CodeUnit), indexes, args => Kernels.CodeUnit(args[0], args[1]));

        using var pe = new PEReader(new MemoryStream(image));
        var metadata = pe.GetMetadataReader();
        int literal = Row(nameof(Kernels.Literal));
        int rva = metadata.GetMethodDefinition(MetadataTokens.MethodDefinitionHandle(literal)).RelativeVirtualAddress;
        byte[] il = pe.GetMethodBody(rva).GetILBytes()!;
        int op = Array.IndexOf(il, (byte)0x72);
        uint token = BinaryPrimitives.ReadUInt32LittleEndian(il.AsSpan(op + 1));
        if (op < 0 || token >> 24 != 0x70)
            throw new InvalidOperationException("Expected an ldstr fixture.");
        int heap = pe.PEHeaders.MetadataStartOffset + metadata.GetHeapMetadataOffset(HeapIndex.UserString);
        int first = heap + (int)(token & 0xffffff);
        byte count = image[first];
        if (count >= 128) throw new InvalidOperationException("Expected a short #US entry.");
        byte[] even = (byte[])image.Clone();
        even[first] = 2;
        Reject("even #US byte count", even);
        byte[] badTerminal = (byte[])image.Clone();
        badTerminal[first + count] = 2;
        Reject("invalid #US terminal flag", badTerminal);
        byte[] unreadable = (byte[])image.Clone();
        unreadable[first] = 0xff;
        Reject("invalid #US compressed length", unreadable);
        byte[] overflow = (byte[])image.Clone();
        overflow[first] = 0xdf;
        overflow[first + 1] = overflow[first + 2] = overflow[first + 3] = 0xff;
        Reject("out-of-range #US payload", overflow);

        int Row(string name) => typeof(Kernels).GetMethod(name)!.MetadataToken & 0xffffff;

        void Verify(string method, IEnumerable<int[]> arguments, Func<int[], int> oracle)
        {
            int row = Row(method);
            int length = StringDemo.Compile(image, row, output);
            if (length <= 0) throw new InvalidOperationException($"{method} returned {length}.");
            string hex = Convert.ToHexString(output.AsSpan(0, length)).ToLowerInvariant();
            var inputs = new object[] { Encode(image), row };
            foreach (int[] args in arguments)
            {
                int? expected;
                try { expected = oracle(args); }
                catch (IndexOutOfRangeException) { expected = null; }
                cases.Add(new
                {
                    module = "StringLibrary", method = "Compile", fixture = method, inputs,
                    generated = new { capacity, arguments = args, hex }, expected
                });
            }
        }

        void Reject(string fixture, byte[] source)
        {
            Array.Fill(output, (byte)0xa5);
            int row = Row(nameof(Kernels.Length));
            if (StringDemo.Compile(source, row, output) != -1 || output.Any(value => value != 0xa5))
                throw new InvalidOperationException($"Compiler did not reject {fixture} without writes.");
            cases.Add(new
            {
                module = "StringLibrary", method = "Compile", fixture,
                inputs = new object[] { Encode(source), row },
                generated = new { capacity, arguments = Array.Empty<int>(), reject = true }, expected = -1
            });
        }
    }

    private static object Encode(byte[] bytes) => new { bytes = Array.ConvertAll(bytes, value => (int)value) };
}
