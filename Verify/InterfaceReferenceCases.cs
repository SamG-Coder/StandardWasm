using System.Buffers.Binary;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using BootstrapLibrary;
using InterfaceContracts;
using InterfaceDemo;

internal static class InterfaceReferenceCases
{
    internal static void Add(List<object> cases)
    {
        const int capacity = 32768;
        byte[] image = File.ReadAllBytes(typeof(Kernels).Assembly.Location);
        byte[] dependency = File.ReadAllBytes(typeof(Holder).Assembly.Location);
        byte[] bundle = BundleCases.Pack(image, dependency);
        byte[] output = new byte[capacity];
        int[] location = CompilerExports.CreateDiagnostic();

        int[][] values = [[int.MinValue], [-5], [0], [5], [int.MaxValue]];
        Check(nameof(Kernels.Run), values, arguments => Kernels.Run(arguments[0]));
        Check(nameof(Kernels.Widen), values, arguments => Kernels.Widen(arguments[0]));
        Check(nameof(Kernels.RejectDispatch), [[]], _ => Kernels.RejectDispatch());
        Reject(bundle, nameof(Kernels.Dispatch), nameof(Kernels.Dispatch), -1, -2);
        if (Kernels.RejectCast(5) != 0) throw new InvalidOperationException("The CLR accepted an unrelated type.");
        Reject(bundle, nameof(Kernels.RejectCast), nameof(Kernels.RejectCast),
            Opcode(nameof(Kernels.RejectCast), OpCodes.Isinst), -2);

        if (Kernels.Store(5).Stored is not Derived { Value: 5 })
            throw new InvalidOperationException("The CLR did not store the implementing object.");
        int storeRow = Row(nameof(Kernels.Store));
        int call = Opcode(nameof(Kernels.Store), OpCodes.Call);
        using var pe = new PEReader(new MemoryStream(image));
        int rva = pe.GetMetadataReader().GetMethodDefinition(MetadataTokens.MethodDefinitionHandle(storeRow)).RelativeVirtualAddress;
        var section = pe.PEHeaders.SectionHeaders[pe.PEHeaders.GetContainingSectionIndex(rva)];
        int start = section.PointerToRawData + rva - section.VirtualAddress;
        int header = (image[start] & 3) == 2 ? 1 : (image[start + 1] >> 4) * 4;
        byte[] mutated = (byte[])image.Clone();
        int replacement = typeof(Kernels).GetMethod(nameof(Kernels.MakeUnrelated))!.MetadataToken;
        BinaryPrimitives.WriteInt32LittleEndian(mutated.AsSpan(start + header + call + 1), replacement);
        Reject(BundleCases.Pack(mutated, dependency), nameof(Kernels.Store), nameof(Kernels.Store),
            Opcode(nameof(Kernels.Store), OpCodes.Stfld), -1);

        void Check(string name, int[][] inputCases, Func<int[], int> clr)
        {
            byte[] entries = Manifest(name);
            int size = CompilerExports.CompileBundleDetailed(bundle, entries, output, location);
            if (size <= 0)
                throw new InvalidOperationException($"{name} returned {size} at [{string.Join(", ", location)}].");
            string hex = Convert.ToHexString(output.AsSpan(0, size)).ToLowerInvariant();
            var inputs = new[] { Encode(bundle), Encode(entries) };
            foreach (int[] arguments in inputCases)
            {
                int? expected = null;
                string exception = null;
                try { expected = clr(arguments); }
                catch (NullReferenceException error) { exception = error.GetType().FullName; }
                cases.Add(new
                {
                    module = "BootstrapLibrary", method = "CompileBundle", fixture = name, inputs,
                    generated = new { capacity, bundle = true, arguments, hex, exceptionType = exception },
                    expected, exception
                });
            }
        }

        void Reject(byte[] input, string entry, string failingMethod, int offset, int expected)
        {
            byte[] entries = Manifest(entry);
            Array.Fill(output, (byte)0xa5);
            int[] diagnostic = [0, Row(failingMethod), offset];
            int status = CompilerExports.CompileBundleDetailed(input, entries, output, location);
            if (status != expected || !location.SequenceEqual(diagnostic) || output.Any(b => b != 0xa5))
                throw new InvalidOperationException($"{entry} returned {status} at [{string.Join(", ", location)}].");
            cases.Add(new
            {
                module = "BootstrapLibrary", method = "CompileBundle", fixture = entry,
                inputs = new[] { Encode(input), Encode(entries) },
                generated = new { capacity, bundle = true, arguments = Array.Empty<int>(), reject = true, diagnostic },
                expected
            });
        }
    }

    private static int Row(string name) => typeof(Kernels).GetMethod(name)!.MetadataToken & 0xffffff;

    private static int Opcode(string name, OpCode opcode)
    {
        byte[] il = typeof(Kernels).GetMethod(name)!.GetMethodBody()!.GetILAsByteArray()!;
        int offset = IlOffsets.Find(il, opcode);
        if (offset < 0) throw new InvalidOperationException($"{name} did not emit {opcode}.");
        return offset;
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
