#nullable enable

using BootstrapLibrary;
using EventLibrary;

internal static class SenderIdentityCases
{
    internal static void Add(List<object> cases)
    {
        const int capacity = 32768;
        byte[] image = File.ReadAllBytes(typeof(SenderCases).Assembly.Location);
        var input = new { bytes = Array.ConvertAll(image, value => (int)value) };

        Check(nameof(SenderCases.SenderIdentity), [0, 1, 2], SenderCases.SenderIdentity);
        Check(nameof(SenderCases.SenderLocal), [-1, 0, 1, 3], SenderCases.SenderLocal);

        void Check(string name, int[] values, Func<int, int> oracle)
        {
            int row = typeof(SenderCases).GetMethod(name)!.MetadataToken & 0xffffff;
            var output = new byte[capacity];
            int length = CompilerExports.Compile(image, row, output);
            if (length <= 0) throw new InvalidOperationException($"{name} returned {length}");
            string hex = Convert.ToHexString(output.AsSpan(0, length)).ToLowerInvariant();

            foreach (int value in values)
                cases.Add(new
                {
                    module = "BootstrapLibrary", method = "Compile", fixture = name,
                    inputs = new object[] { input, row },
                    generated = new { capacity, arguments = new[] { value }, hex },
                    expected = oracle(value)
                });
        }
    }
}
