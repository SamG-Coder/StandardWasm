#nullable enable

using BootstrapLibrary;
using NullAccess;

internal static class NullAccessCases
{
    internal static void Add(List<object> cases)
    {
        const int capacity = 16384;
        byte[] image = File.ReadAllBytes(typeof(Kernels).Assembly.Location);
        object encoded = new { bytes = Array.ConvertAll(image, value => (int)value) };
        int[][] positions = [[0, -1], [0, 0], [0, int.MaxValue], [1, -1], [1, 0], [1, 1]];
        Check(nameof(Kernels.ReadArray), positions, args => Kernels.ReadArray(args[0], args[1]));
        Check(nameof(Kernels.Character), positions, args => Kernels.Character(args[0], args[1]));
        Check(nameof(Kernels.WriteArray), [[-1], [0], [int.MaxValue]], args => Kernels.WriteArray(args[0]));
        Check(nameof(Kernels.ArrayLength), [[0], [1]], args => Kernels.ArrayLength(args[0]));
        Check(nameof(Kernels.ReadField), [[0], [1]], args => Kernels.ReadField(args[0]));
        Check(nameof(Kernels.WriteField), [[0], [1]], args => Kernels.WriteField(args[0]));
        Check(nameof(Kernels.StringLength), [[0], [1]], args => Kernels.StringLength(args[0]));
        Check(nameof(Kernels.ConcatNullable), [[0], [1]], args => Kernels.ConcatNullable(args[0]));

        void Check(string method, int[][] fixtures, Func<int[], int> oracle)
        {
            int row = typeof(Kernels).GetMethod(method)!.MetadataToken & 0xffffff;
            var output = new byte[capacity];
            int length = CompilerExports.Compile(image, row, output);
            if (length <= 0) throw new InvalidOperationException($"{method} compiler returned {length}");
            string hex = Convert.ToHexString(output.AsSpan(0, length)).ToLowerInvariant();
            foreach (int[] arguments in fixtures)
            {
                int? expected = null;
                string? exception = null;
                try { expected = oracle(arguments); }
                catch (Exception error) when (error is NullReferenceException or IndexOutOfRangeException)
                {
                    exception = error.GetType().FullName;
                }
                cases.Add(new
                {
                    module = "BootstrapLibrary", method = "Compile", fixture = method,
                    inputs = new[] { encoded, row }, generated = new { capacity, arguments, hex, exceptionType = exception },
                    expected, exception
                });
            }
        }
    }
}
