using BootstrapLibrary;
using NarrowScalars;

internal static class PortableNarrowScalarCases
{
    internal static void Add(List<object> cases)
    {
        const int capacity = 8192;
        byte[] image = File.ReadAllBytes(typeof(ScalarCases).Assembly.Location);
        var input = new { bytes = Array.ConvertAll(image, value => (int)value) };
        int[] limits = [int.MinValue, -65537, -32769, -129, -1, 0, 127, 128, 255, 32768, 65535, 65536, int.MaxValue];
        Check(nameof(ScalarCases.ReadSigned), limits.Select(value =>
            (new[] { value, value }, ScalarCases.ReadSigned(unchecked((sbyte)value), unchecked((short)value)))));
        Check(nameof(ScalarCases.ReadUnsigned), limits.Select(value =>
            (new[] { value, value, value }, ScalarCases.ReadUnsigned(unchecked((byte)value), unchecked((ushort)value), unchecked((char)value)))));
        Check(nameof(ScalarCases.WrapSigned), limits.Select(value => (new[] { value }, (int)ScalarCases.WrapSigned(value))));
        Check(nameof(ScalarCases.WrapUnsigned), limits.Select(value => (new[] { value }, (int)ScalarCases.WrapUnsigned(value))));
        Check(nameof(ScalarCases.Compose), limits.Select(value => (new[] { value }, ScalarCases.Compose(value))));
        Check(nameof(ScalarCases.Count), new[] { -1, 0, 1, 32, 33, 66, 130 }
            .Select(steps => (new[] { steps }, ScalarCases.Count(steps))));

        void Check(string method, IEnumerable<(int[] Arguments, int Expected)> tests)
        {
            int row = typeof(ScalarCases).GetMethod(method)!.MetadataToken & 0xffffff;
            var output = new byte[capacity];
            int length = CompilerExports.Compile(image, row, output);
            if (length <= 0) throw new InvalidOperationException($"{method} returned {length}");
            string hex = Convert.ToHexString(output.AsSpan(0, length)).ToLowerInvariant();
            foreach (var (arguments, expected) in tests)
                cases.Add(new
                {
                    module = "BootstrapLibrary", method = "Compile", fixture = method,
                    inputs = new object[] { input, row }, generated = new { capacity, arguments, hex }, expected
                });
        }
    }
}
