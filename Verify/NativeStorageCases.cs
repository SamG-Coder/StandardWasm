using System.Buffers.Binary;
using System.Globalization;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Text.Json;
using BootstrapLibrary;
using NativeLayout;
using NativeLayoutDependency;

internal static class NativeStorageCases
{
    internal static void Add(List<object> cases)
    {
        const int capacity = 32768;
        byte[] image = File.ReadAllBytes(typeof(Kernels).Assembly.Location);
        byte[] dependency = File.ReadAllBytes(typeof(NativeCell).Assembly.Location);
        byte[] bundle = BundleCases.Pack(image, dependency);
        byte[] output = new byte[capacity];
        int[] location = CompilerExports.CreateDiagnostic();
        var compiled = new Dictionary<string, (object[] Inputs, string Hex)>();

        foreach (WidthResult result in ReadWidths())
        {
            Check(nameof(Kernels.SignedRoundTrip), [Wide(result.Input)], Wide(result.Signed));
            Check(nameof(Kernels.UnsignedRoundTrip), [Wide(result.Input)], Wide(result.Unsigned));
            Checked(nameof(Kernels.CheckedSigned), result.Input, () =>
                Kernels.CheckedSigned(IntPtr.Size == 4 ? result.Input : checked((int)result.Input)));
            Checked(nameof(Kernels.CheckedUnsigned), result.Input, () =>
                Kernels.CheckedUnsigned(IntPtr.Size == 4 ? unchecked((ulong)result.Input) : checked((uint)unchecked((ulong)result.Input))));
        }
        foreach (int input in new[] { int.MinValue, -1, 0, 1, int.MaxValue })
        {
            Check(nameof(Kernels.Run), [input], Kernels.Run(input));
            Check(nameof(Kernels.Compare), [input], Kernels.Compare(input));
        }
        foreach (var (length, index) in new[] { (1, 0), (4, 2), (127, 126) })
            Check(nameof(Kernels.ArrayAt), [length, index], Kernels.ArrayAt(length, index));
        foreach (int length in new[] { 0, 1, 127 })
            Check(nameof(Kernels.HasArrayItems), [length], Kernels.HasArrayItems(length) ? 1 : 0);

        Reject(bundle, nameof(Kernels.RejectNativeAdd), Offset(nameof(Kernels.RejectNativeAdd), OpCodes.Add), -2);

        int row = Row(nameof(Kernels.Run));
        using var pe = new PEReader(new MemoryStream(image));
        int rva = pe.GetMetadataReader().GetMethodDefinition(MetadataTokens.MethodDefinitionHandle(row)).RelativeVirtualAddress;
        var section = pe.PEHeaders.SectionHeaders[pe.PEHeaders.GetContainingSectionIndex(rva)];
        int body = section.PointerToRawData + rva - section.VirtualAddress;
        int header = (image[body] & 3) == 2 ? 1 : (image[body + 1] >> 4) * 4;
        byte[] malformed = (byte[])image.Clone();
        malformed[body + header + Offset(nameof(Kernels.Run), OpCodes.Conv_I)] = (byte)OpCodes.Nop.Value;
        Reject(BundleCases.Pack(malformed, dependency), nameof(Kernels.Run), Offset(nameof(Kernels.Run), OpCodes.Stfld), -1);

        void Checked(string name, long input, Func<long> clr)
        {
            object expected;
            try { expected = Wide(clr()); }
            catch (OverflowException)
            {
                Check(name, [Wide(input)], null, typeof(OverflowException).FullName);
                return;
            }
            Check(name, [Wide(input)], expected);
        }

        void Check(string name, object[] arguments, object expected, string exception = null)
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
            cases.Add(new
            {
                module = "BootstrapLibrary", method = "CompileBundle", fixture = "native storage " + name,
                inputs = module.Inputs, generated = new { capacity, bundle = true, arguments, hex = module.Hex, exceptionType = exception },
                expected, exception
            });
        }

        void Reject(byte[] input, string name, int offset, int expected)
        {
            byte[] entries = Manifest(name);
            int[] diagnostic = [0, Row(name), offset];
            Array.Fill(output, (byte)0xa5);
            int status = CompilerExports.CompileBundleDetailed(input, entries, output, location);
            if (status != expected || !location.SequenceEqual(diagnostic) || output.Any(value => value != 0xa5))
                throw new InvalidOperationException($"{name} returned {status} at [{string.Join(", ", location)}].");
            cases.Add(new
            {
                module = "BootstrapLibrary", method = "CompileBundle", fixture = "native storage rejection " + name,
                inputs = new[] { Encode(input), Encode(entries) },
                generated = new { capacity, bundle = true, arguments = Array.Empty<int>(), reject = true, diagnostic },
                expected = status
            });
        }
    }

    internal static void Write32BitOracle(string path)
    {
        if (IntPtr.Size != 4) throw new InvalidOperationException("This oracle requires a 32-bit CLR.");
        var widths = ReadWidths().Select(result => new { Input = result.Input.ToString(CultureInfo.InvariantCulture),
            result.Signed, result.Unsigned });
        File.WriteAllText(path, JsonSerializer.Serialize(widths));
    }

    private static WidthResult[] ReadWidths()
    {
        long[] values = [long.MinValue, int.MinValue - 1L, int.MinValue, -1, 0, 1,
            int.MaxValue, int.MaxValue + 1L, uint.MaxValue, uint.MaxValue + 1L, long.MaxValue];
        return values.Select(value => new WidthResult(value,
            Kernels.SignedRoundTrip(IntPtr.Size == 4 ? value : unchecked((int)value)),
            Kernels.UnsignedRoundTrip(IntPtr.Size == 4 ? value : unchecked((uint)value)))).ToArray();
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
    private static object Wide(long value) => new { i64 = value.ToString(CultureInfo.InvariantCulture) };
    private readonly record struct WidthResult(long Input, long Signed, long Unsigned);
}
