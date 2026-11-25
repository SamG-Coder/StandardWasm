using ControlStorageLibrary;

internal static class ControlStorageCases
{
    internal static void Add(List<object> cases)
    {
        const int capacity = 16384;
        byte[] image = File.ReadAllBytes(typeof(Kernels).Assembly.Location);
        int row = typeof(Kernels).GetMethod(nameof(Kernels.Change))!.MetadataToken & 0xffffff;
        byte[] output = new byte[capacity];
        int length = ControlStorageDemo.Compile(image, row, output);
        if (length <= 0) throw new InvalidOperationException($"Reference-field fixture returned {length}.");
        string hex = Convert.ToHexString(output.AsSpan(0, length)).ToLowerInvariant();
        var inputs = new object[] { new { bytes = Array.ConvertAll(image, value => (int)value) }, row };
        foreach (int value in new[] { int.MinValue, -257, -1, 0, 127, 128, 255, 256, 511, int.MaxValue })
            cases.Add(new
            {
                module = "ControlStorageLibrary", method = "Compile", fixture = "typed array fields",
                inputs, generated = new { capacity, arguments = new[] { value }, hex },
                expected = Kernels.Change(value)
            });
    }
}
