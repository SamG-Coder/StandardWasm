using System.Buffers.Binary;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using BootstrapLibrary;
using GenericStorageDemo;
using GenericStorageDependency;

internal static class GenericLayoutCases
{
    internal static void Add(List<object> cases)
    {
        const int capacity = 32768;
        byte[] image = File.ReadAllBytes(typeof(Kernels).Assembly.Location);
        byte[] dependency = File.ReadAllBytes(typeof(Frame).Assembly.Location);
        byte[] bundle = BundleCases.Pack(image, dependency);
        byte[] output = new byte[capacity];
        int[] location = CompilerExports.CreateDiagnostic();
        var compiled = new Dictionary<string, (object[] Inputs, string Hex)>();

        foreach (int value in new[] { int.MinValue, -5, 0, 5, int.MaxValue })
        {
            Check(nameof(Kernels.Run), [value], () => Kernels.Run(value));
            Check(nameof(Kernels.Store), [value], () => Kernels.Store(value));
        }
        foreach (var (index, clear) in new[] { (0, false), (-1, false), (1, false), (-1, true) })
            Check(nameof(Kernels.ReadArray), [index, clear ? 1 : 0], () => Kernels.ReadArray(index, clear));

        int store = Row(nameof(Kernels.Store));
        using var pe = new PEReader(new MemoryStream(image));
        int rva = pe.GetMetadataReader().GetMethodDefinition(MetadataTokens.MethodDefinitionHandle(store)).RelativeVirtualAddress;
        var section = pe.PEHeaders.SectionHeaders[pe.PEHeaders.GetContainingSectionIndex(rva)];
        int body = section.PointerToRawData + rva - section.VirtualAddress;
        int header = (image[body] & 3) == 2 ? 1 : (image[body + 1] >> 4) * 4;
        byte[] changed = (byte[])image.Clone();
        int call = Offset(nameof(Kernels.Store), OpCodes.Call);
        int replacement = typeof(Kernels).GetMethod(nameof(Kernels.ReadRight))!.MetadataToken;
        BinaryPrimitives.WriteInt32LittleEndian(changed.AsSpan(body + header + call + 1), replacement);
        Reject("distinct instantiation", BundleCases.Pack(changed, dependency), nameof(Kernels.Store), OpCodes.Stfld, -1);
        if (Kernels.ReadConstructed(5) != 5)
            throw new InvalidOperationException("The CLR generic constructor fixture did not execute.");
        Reject("specialized constructor", bundle, nameof(Kernels.ReadConstructed), OpCodes.Newobj, -2);

        void Check(string name, int[] arguments, Func<int> clr)
        {
            if (!compiled.TryGetValue(name, out var module))
            {
                byte[] entries = Manifest(name);
                int size = CompilerExports.CompileBundleDetailed(bundle, entries, output, location);
                if (size <= 0)
                    throw new InvalidOperationException($"{name} returned {size} at [{string.Join(", ", location)}].");
                module = ([Encode(bundle), Encode(entries)], Convert.ToHexString(output.AsSpan(0, size)).ToLowerInvariant());
                compiled.Add(name, module);
            }
            int? expected = null;
            string exception = null;
            try { expected = clr(); }
            catch (Exception error) when (error is NullReferenceException or IndexOutOfRangeException)
            {
                exception = error.GetType().FullName;
            }
            cases.Add(new
            {
                module = "BootstrapLibrary", method = "CompileBundle", fixture = "generic layout " + name,
                inputs = module.Inputs, generated = new { capacity, bundle = true, arguments, hex = module.Hex, exceptionType = exception },
                expected, exception
            });
        }

        void Reject(string label, byte[] input, string name, OpCode opcode, int expected)
        {
            byte[] entries = Manifest(name);
            int[] diagnostic = [0, Row(name), Offset(name, opcode)];
            Array.Fill(output, (byte)0xa5);
            int status = CompilerExports.CompileBundleDetailed(input, entries, output, location);
            if (status != expected || !location.SequenceEqual(diagnostic) || output.Any(b => b != 0xa5))
                throw new InvalidOperationException($"{label} returned {status} at [{string.Join(", ", location)}].");
            cases.Add(new
            {
                module = "BootstrapLibrary", method = "CompileBundle", fixture = "generic layout " + label,
                inputs = new[] { Encode(input), Encode(entries) },
                generated = new { capacity, bundle = true, arguments = Array.Empty<int>(), reject = true, diagnostic },
                expected = status
            });
        }
    }

    private static int Row(string name) => typeof(Kernels).GetMethod(name)!.MetadataToken & 0xffffff;

    private static int Offset(string name, OpCode opcode)
    {
        byte[] il = typeof(Kernels).GetMethod(name)!.GetMethodBody()!.GetILAsByteArray()!;
        int offset = IlOffsets.Find(il, opcode);
        return offset >= 0 ? offset : throw new InvalidOperationException($"{name} did not emit {opcode}.");
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
