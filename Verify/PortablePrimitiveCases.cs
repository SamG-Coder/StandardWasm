#nullable enable

using System.Globalization;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using BootstrapLibrary;
using PrimitiveStorage;

internal static class PortablePrimitiveCases
{
    internal static void Add(List<object> cases)
    {
        const int capacity = 8192;
        byte[] image = File.ReadAllBytes(typeof(StorageCases).Assembly.Location);
        var input = new { bytes = Array.ConvertAll(image, value => (int)value) };
        int[] limits = [int.MinValue, -65537, -32769, -129, -1, 0, 127, 128, 32768, 65535, 65536, int.MaxValue];
        CheckIntegers(nameof(StorageCases.ReadSigned), limits, StorageCases.ReadSigned);
        CheckIntegers(nameof(StorageCases.ReadUnsigned), limits, StorageCases.ReadUnsigned);
        CheckIntegers(nameof(StorageCases.ReadSignedByte), limits, StorageCases.ReadSignedByte);
        CheckIntegers(nameof(StorageCases.ReadUnsignedBits), limits, StorageCases.ReadUnsignedBits);
        int[] lengths = [-1, 0, 1, 11, 128];
        CheckIntegers(nameof(StorageCases.PackedLength), lengths, StorageCases.PackedLength);
        CheckIntegers(nameof(StorageCases.CountFlags), lengths, StorageCases.CountFlags);

        double[] ieee = [-0.0, double.Epsilon, -float.Epsilon, 0.1, double.MaxValue, double.PositiveInfinity, double.NegativeInfinity, double.NaN];
        Check(nameof(StorageCases.ReadSingle), from value in ieee from index in new[] { -1, 0, 1, 2 }
            select (new object[] { Real((float)value), index }, (Func<object>)(() => Real(StorageCases.ReadSingle((float)value, index)))));
        Check(nameof(StorageCases.ReadDouble), from value in ieee from index in new[] { -1, 0, 1, 2 }
            select (new object[] { Real(value), index }, (Func<object>)(() => Real(StorageCases.ReadDouble(value, index)))));

        // Keep the name and change only the platform token to exercise newarr's
        // AssemblyRef check independently of the primitive's storage width.
        using var pe = new PEReader(new MemoryStream(image));
        var metadata = pe.GetMetadataReader();
        var netstandard = metadata.AssemblyReferences.Select(metadata.GetAssemblyReference)
            .Single(reference => metadata.GetString(reference.Name) == "netstandard");
        int token = pe.PEHeaders.MetadataStartOffset + metadata.GetHeapMetadataOffset(HeapIndex.Blob) +
            MetadataTokens.GetHeapOffset(netstandard.PublicKeyOrToken);
        byte[] spoofed = (byte[])image.Clone();
        if (spoofed[token] != 8) throw new InvalidDataException("Expected an eight-byte assembly token");
        spoofed[token + 1] ^= 1;
        int row = Row(nameof(StorageCases.ReadSigned));
        byte[] rejected = Enumerable.Repeat((byte)0xa5, capacity).ToArray();
        int status = CompilerExports.Compile(spoofed, row, rejected);
        if (status != -2 || rejected.Any(value => value != 0xa5))
            throw new InvalidOperationException("Spoofed primitive newarr was not rejected without writes");
        cases.Add(new
        {
            module = "BootstrapLibrary", method = "Compile", fixture = "newarr with wrong AssemblyRef token",
            inputs = new object[] { new { bytes = Array.ConvertAll(spoofed, value => (int)value) }, row },
            generated = new { capacity, arguments = Array.Empty<int>(), reject = true }, expected = status
        });

        void CheckIntegers(string method, int[] values, Func<int, int> read) =>
            Check(method, values.Select(value => (new object[] { value }, (Func<object>)(() => read(value)))));

        void Check(string method, IEnumerable<(object[] Arguments, Func<object> Read)> tests)
        {
            int methodRow = Row(method);
            var output = new byte[capacity];
            int length = CompilerExports.Compile(image, methodRow, output);
            if (length <= 0) throw new InvalidOperationException($"{method} returned {length}");
            string hex = Convert.ToHexString(output.AsSpan(0, length)).ToLowerInvariant();
            foreach (var (arguments, read) in tests)
            {
                object? expected;
                string? exception = null;
                try { expected = read(); }
                catch (Exception error) when (error is ArithmeticException or IndexOutOfRangeException)
                {
                    expected = null;
                    exception = error.GetType().FullName;
                }
                cases.Add(new
                {
                    module = "BootstrapLibrary", method = "Compile", fixture = method,
                    inputs = new object[] { input, methodRow }, generated = new { capacity, arguments, hex, exceptionType = exception },
                    expected, exception
                });
            }
        }
    }

    private static int Row(string method) => typeof(StorageCases).GetMethod(method)!.MetadataToken & 0xffffff;
    private static object Real(double value) => new { f64 = value.ToString("G17", CultureInfo.InvariantCulture) };
}
