using System.Globalization;
using System.Text;
using FormattingLibrary;

internal static class FormattingCases
{
    internal static void Add(List<object> cases)
    {
        const int capacity = 32768;
        byte[] image = File.ReadAllBytes(typeof(Kernels).Assembly.Location);
        var outputs = new byte[capacity];
        using var manifest = new MemoryStream();
        using var writer = new BinaryWriter(manifest);
        string[] exports = ["Format", "Join", "CopyThenMutate", "FromNullableArray", "TextLength", "TextCodeUnit"];
        foreach (string name in exports)
        {
            byte[] encoded = Encoding.ASCII.GetBytes(name);
            writer.Write(typeof(Kernels).GetMethod(name)!.MetadataToken & 0xffffff);
            writer.Write(encoded.Length);
            writer.Write(encoded);
        }

        byte[] entries = manifest.ToArray();
        int length = FormattingDemo.CompileModule(image, entries, outputs);
        if (length <= 0)
            throw new InvalidOperationException($"Formatting compiler returned {length}.");
        string hex = Convert.ToHexString(outputs.AsSpan(0, length)).ToLowerInvariant();
        object[] inputs = [Encode(image), Encode(entries)];
        foreach (int n in new[] { int.MinValue, int.MinValue + 1, -1000000000, -100, -10, -9, -1, 0,
                     1, 9, 10, 99, 100, 1000000000, int.MaxValue - 1, int.MaxValue })
        {
            string expected = n.ToString(CultureInfo.InvariantCulture);
            if (Kernels.Format(n) != expected)
                throw new InvalidOperationException($"Managed formatter disagrees with the CLR for {n}.");
            AddCase("Format", [n], expected);
        }
        for (int n = 0; n <= 4; n++)
            AddCase("Join", [n], Kernels.Join(n));
        foreach (int cp in new[] { 0, 65, 0x03a9, 0xffff })
            AddCase("CopyThenMutate", [cp], Kernels.CopyThenMutate(cp));
        AddCase("FromNullableArray", [null], new string((char[])null));

        void AddCase(string export, object[] arguments, string expected)
        {
            cases.Add(new
            {
                module = "FormattingLibrary", method = "CompileModule", fixture = export, inputs,
                generated = new { capacity, arguments, hex, multiExport = true, export, resultType = "string" },
                expected
            });
        }
    }

    private static object Encode(byte[] bytes) => new { bytes = Array.ConvertAll(bytes, value => (int)value) };
}
