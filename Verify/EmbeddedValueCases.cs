using System.Buffers.Binary;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using BootstrapLibrary;
using EmbeddedValueDemo;
using EmbeddedValueDependency;

internal static class EmbeddedValueCases
{
    internal static void Add(List<object> cases)
    {
        const int capacity = 32768;
        byte[] image = File.ReadAllBytes(typeof(Kernels).Assembly.Location);
        byte[] dependency = File.ReadAllBytes(typeof(Frame).Assembly.Location);
        byte[] bundle = BundleCases.Pack(image, dependency);
        byte[] output = new byte[capacity];
        int[] location = CompilerExports.CreateDiagnostic();

        Compare(nameof(Kernels.Run), Kernels.Run);
        Compare(nameof(Kernels.Reset), Kernels.Reset);
        Compare(nameof(Kernels.Assign), Kernels.Assign);

        string read = nameof(Kernels.ReadReceiver);
        var (readInputs, readHex) = Compile(read);
        try
        {
            Kernels.ReadReceiver(null);
            throw new InvalidOperationException("The CLR did not check the containing reference.");
        }
        catch (NullReferenceException error)
        {
            string exception = error.GetType().FullName!;
            cases.Add(new
            {
                module = "BootstrapLibrary", method = "CompileBundle", fixture = "embedded value null receiver", inputs = readInputs,
                generated = new { capacity, bundle = true, arguments = new object[] { null }, hex = readHex, exceptionType = exception },
                expected = (int?)null, exception
            });
        }

        string borrow = nameof(Kernels.RejectBorrow);
        byte[] borrowIl = IL(borrow);
        int afterInitAddress = Offset(borrow, OpCodes.Ldloca_S) + 2;
        int borrowed = IlOffsets.Find(borrowIl[afterInitAddress..], OpCodes.Ldloca_S);
        if (borrowed < 0) throw new InvalidOperationException("No escaping local address in the fixture.");
        Reject("escaping borrow", bundle, borrow, afterInitAddress + borrowed, -2);
        Reject("exported value", bundle, nameof(Kernels.RejectExport), -1, -2);

        string assign = nameof(Kernels.Assign);
        using var pe = new PEReader(new MemoryStream(image));
        int rva = pe.GetMetadataReader().GetMethodDefinition(MetadataTokens.MethodDefinitionHandle(Row(assign))).RelativeVirtualAddress;
        var section = pe.PEHeaders.SectionHeaders[pe.PEHeaders.GetContainingSectionIndex(rva)];
        int body = section.PointerToRawData + rva - section.VirtualAddress;
        int header = (image[body] & 3) == 2 ? 1 : (image[body + 1] >> 4) * 4;
        byte[] other = (byte[])image.Clone();
        BinaryPrimitives.WriteInt32LittleEndian(other.AsSpan(body + header + Offset(assign, OpCodes.Call) + 1),
            typeof(Kernels).GetMethod(nameof(Kernels.MakeOther))!.MetadataToken);
        Reject("same layout different value type", BundleCases.Pack(other, dependency), assign, Offset(assign, OpCodes.Stfld), -1);

        void Compare(string name, Func<int, int> clr)
        {
            var (inputs, hex) = Compile(name);
            foreach (int value in new[] { int.MinValue, -32769, 0, 5, 32767, int.MaxValue })
                cases.Add(new
                {
                    module = "BootstrapLibrary", method = "CompileBundle", fixture = "embedded value " + name, inputs,
                    generated = new { capacity, bundle = true, arguments = new[] { value }, hex }, expected = clr(value)
                });
        }

        (object[] Inputs, string Hex) Compile(string name)
        {
            byte[] entries = Manifest(name);
            int length = CompilerExports.CompileBundleDetailed(bundle, entries, output, location);
            if (length <= 0)
                throw new InvalidOperationException($"{name} returned {length} at [{string.Join(", ", location)}].");
            return ([Encode(bundle), Encode(entries)], Convert.ToHexString(output.AsSpan(0, length)).ToLowerInvariant());
        }

        void Reject(string label, byte[] input, string name, int offset, int expected)
        {
            byte[] entries = Manifest(name);
            int[] diagnostic = [0, Row(name), offset];
            Array.Fill(output, (byte)0xa5);
            int status = CompilerExports.CompileBundleDetailed(input, entries, output, location);
            if (status != expected || !location.SequenceEqual(diagnostic) || output.Any(b => b != 0xa5))
                throw new InvalidOperationException($"{label} returned {status} at [{string.Join(", ", location)}].");
            cases.Add(new
            {
                module = "BootstrapLibrary", method = "CompileBundle", fixture = "embedded value " + label,
                inputs = new[] { Encode(input), Encode(entries) },
                generated = new { capacity, bundle = true, arguments = Array.Empty<int>(), reject = true, diagnostic },
                expected = status
            });
        }
    }

    private static byte[] IL(string name) => typeof(Kernels).GetMethod(name)!.GetMethodBody()!.GetILAsByteArray()!;
    private static int Row(string name) => typeof(Kernels).GetMethod(name)!.MetadataToken & 0xffffff;

    private static int Offset(string method, OpCode opcode)
    {
        int offset = IlOffsets.Find(IL(method), opcode);
        return offset >= 0 ? offset : throw new InvalidOperationException($"{method} did not emit {opcode}.");
    }

    private static byte[] Manifest(string name)
    {
        byte[] entries = new byte[11];
        BinaryPrimitives.WriteInt32LittleEndian(entries, Row(name));
        entries[4] = 3;
        "run"u8.CopyTo(entries.AsSpan(8));
        return entries;
    }

    private static object Encode(byte[] bytes) => new { bytes = Array.ConvertAll(bytes, value => (int)value) };
}
