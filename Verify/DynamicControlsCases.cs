using DynamicControlsLibrary;

internal static class DynamicControlsCases
{
    internal static void Add(List<object> cases)
    {
        const int capacity = 32768;
        byte[] image = File.ReadAllBytes(typeof(Kernels).Assembly.Location);
        var encoded = new { bytes = Array.ConvertAll(image, value => (int)value) };
        Test(nameof(Kernels.Toggle), Kernels.Toggle, [-1, 0, 1, 2, 3, 10, 11, 200, 201]);
        Test(nameof(Kernels.Compact), Kernels.Compact, [0, 10, 204, int.MaxValue]);

        void Test(string method, Func<int, int> reference, int[] values)
        {
            int row = typeof(Kernels).GetMethod(method)!.MetadataToken & 0xffffff;
            var output = new byte[capacity];
            int size = DynamicControlsDemo.Compile(image, row, output);
            if (size <= 0)
                throw new InvalidOperationException($"{method} returned {size}.");
            string hex = Convert.ToHexString(output.AsSpan(0, size)).ToLowerInvariant();
            foreach (int value in values)
                cases.Add(new
                {
                    module = "DynamicControlsLibrary", method = "Compile", fixture = method,
                    inputs = new object[] { encoded, row },
                    generated = new { capacity, arguments = new[] { value }, hex }, expected = reference(value)
                });
        }
    }
}
