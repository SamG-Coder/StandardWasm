using BootstrapLibrary;
using ResizeLibrary;

internal static class ResizeCases
{
    internal static void Add(List<object> cases)
    {
        const int capacity = 32768;
        byte[] image = File.ReadAllBytes(typeof(LayoutCases).Assembly.Location);
        var encoded = new { bytes = Array.ConvertAll(image, value => (int)value) };
        int row = typeof(LayoutCases).GetMethod(nameof(LayoutCases.ReadBounds))!.MetadataToken & 0xffffff;
        var output = new byte[capacity];
        int size = CompilerExports.Compile(image, row, output);
        if (size <= 0) throw new InvalidOperationException($"ReadBounds returned {size}");
        string hex = Convert.ToHexString(output.AsSpan(0, size)).ToLowerInvariant();

        foreach ((int width, int height, int finalWidth, int finalHeight) in new[]
        {
            (420, 240, 520, 300), (520, 300, 480, 280), (520, 300, 420, 240),
            (480, 280, 480, 280), (1, 10, 480, 280), (520, 300, 0, 0)
        })
        {
            foreach (int events in new[] { 0, 1, 2 })
            {
                Check(0, 2); // Label X after clicks.
                Check(0, 4); // Label width after resize.
                Check(1, 3); // Left-hand button Y.
                Check(2, 2); // Optional right-hand button X.
                Check(2, 3); // Optional right-hand button Y.

                void Check(int index, int column) =>
                    cases.Add(new
                    {
                        module = "BootstrapLibrary", method = "Compile", fixture = "ReadBounds",
                        inputs = new object[] { encoded, row },
                        generated = new
                        {
                            capacity, hex,
                            arguments = new[] { width, height, finalWidth, finalHeight, events, index, column }
                        },
                        expected = LayoutCases.ReadBounds(width, height, finalWidth, finalHeight, events, index, column)
                    });
            }
        }
    }
}
