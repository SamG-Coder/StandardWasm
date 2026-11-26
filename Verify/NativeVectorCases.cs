#nullable enable

using System.Buffers.Binary;
using System.Reflection.Emit;
using BootstrapLibrary;
using NativeIntegerVectors;

internal static class NativeVectorCases
{
    internal static void Add(List<object> cases)
    {
        foreach (int value in new[] { int.MinValue, -42, -1, 0, 1, 42, int.MaxValue })
        {
            Record(nameof(Kernels.Signed), [value], () => Kernels.Signed(value));
            Record(nameof(Kernels.Unsigned), [value], () => Kernels.Unsigned(value));
            Record(nameof(Kernels.ZeroStore), [value], () => Kernels.ZeroStore(value));
        }
        foreach ((int length, int index) in new[] { (-1, 0), (0, 0), (1, -1), (1, 0), (2, 1), (2, 2) })
            Record(nameof(Kernels.Bounds), [length, index], () => Kernels.Bounds(length, index));
        foreach (int allocate in new[] { 0, 1 })
            Record(nameof(Kernels.NullLoad), [allocate], () => Kernels.NullLoad(allocate));

        const int capacity = 8192;
        byte[] library = Image(typeof(Kernels));
        byte[] linked = BundleCases.Pack(Image(typeof(LinkedNativeVectors.Entry)), library);
        var method = typeof(LinkedNativeVectors.Entry).GetMethod(nameof(LinkedNativeVectors.Entry.FieldAlias))!;
        var entries = new byte[11];
        BinaryPrimitives.WriteInt32LittleEndian(entries, method.MetadataToken & 0xffffff);
        entries[4] = 3;
        "run"u8.CopyTo(entries.AsSpan(8));
        var output = new byte[capacity];
        int lengthWritten = CompilerExports.CompileBundle(linked, entries, output);
        if (lengthWritten < 1) throw new InvalidOperationException($"Native vector bundle returned {lengthWritten}.");
        string hex = Convert.ToHexString(output.AsSpan(0, lengthWritten)).ToLowerInvariant();
        foreach (int input in new[] { int.MinValue, -1, 0, 42, int.MaxValue })
            cases.Add(new
            {
                module = "BootstrapLibrary", method = "CompileBundle", fixture = "linked native vector field alias",
                inputs = new[] { Encode(linked), Encode(entries) },
                generated = new { capacity, bundle = true, arguments = new[] { input }, hex },
                expected = LinkedNativeVectors.Entry.FieldAlias(input)
            });

        var address = typeof(AddressGuard).GetMethod(nameof(AddressGuard.ReadAddress))!;
        int addressRow = address.MetadataToken & 0xffffff;
        int addressIl = IlOffsets.Find(address.GetMethodBody()!.GetILAsByteArray()!, OpCodes.Ldelema);
        int[] diagnostic = [0, addressRow, addressIl];
        Array.Fill(output, (byte)0xa5);
        int[] actual = CompilerExports.CreateDiagnostic();
        int result = CompilerExports.CompileDetailed(library, addressRow, output, actual);
        if (addressIl < 0 || result != -2 || !actual.SequenceEqual(diagnostic) || output.Any(value => value != 0xa5))
            throw new InvalidOperationException($"Native element address returned {result} at [{string.Join(",", actual)}].");
        cases.Add(new
        {
            module = "BootstrapLibrary", method = "Compile", fixture = "native element address",
            inputs = new object[] { Encode(library), addressRow },
            generated = new { capacity, arguments = Array.Empty<int>(), reject = true, diagnostic },
            expected = result
        });

        void Record(string name, int[] inputs, Func<int> oracle)
        {
            int? expected = null;
            string? exception = null;
            try { expected = oracle(); }
            catch (Exception error) when (error is IndexOutOfRangeException or NullReferenceException or OverflowException)
            {
                exception = error.GetType().FullName;
            }
            cases.Add(new { module = "NativeIntegerVectors", method = name, inputs, expected, exception });
        }
    }

    private static byte[] Image(Type type) => File.ReadAllBytes(type.Assembly.Location);

    private static object Encode(byte[] image) => new { bytes = Array.ConvertAll(image, value => (int)value) };
}
