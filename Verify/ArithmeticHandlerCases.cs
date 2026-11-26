#nullable enable

using ArithmeticHandlers;
using BootstrapLibrary;

internal static class ArithmeticHandlerCases
{
    internal static void Add(List<object> cases)
    {
        const int capacity = 8192;
        byte[] image = File.ReadAllBytes(typeof(Handlers).Assembly.Location);
        var encoded = new { bytes = Array.ConvertAll(image, value => (int)value) };
        (int A, int B)[] signed = [(18, 2), (-18, 4), (18, 0), (int.MinValue, -1)];
        CheckPairs(nameof(Handlers.SpecificThenBase), signed, Handlers.SpecificThenBase);
        CheckPairs(nameof(Handlers.ExactSiblings), signed, Handlers.ExactSiblings);
        CheckPairs(nameof(Handlers.BaseCatch), signed, Handlers.BaseCatch);
        CheckPairs(nameof(Handlers.ExactOnly), signed, Handlers.ExactOnly);
        CheckPairs(nameof(Handlers.SiblingDoesNotCatchHandler),
            [(8, 2), (18, 0), (int.MaxValue, 0), (int.MinValue, -1)], Handlers.SiblingDoesNotCatchHandler);
        CheckPairs(nameof(Handlers.UnsignedDivide), [(-1, 0), (-1, 1), (-1, 2), (18, 2)],
            (a, b) => unchecked((int)Handlers.UnsignedDivide((uint)a, (uint)b)));
        Check(nameof(Handlers.ArrayAccess), new[] { -1, 0, 1 }.Select(i =>
            (new[] { i }, (Func<int>)(() => Handlers.ArrayAccess(i)))));

        int nested = typeof(Handlers).GetMethod(nameof(Handlers.Nested))!.MetadataToken & 0xffffff;
        var untouched = Enumerable.Repeat((byte)0xa5, capacity).ToArray();
        var location = CompilerExports.CreateDiagnostic();
        int status = CompilerExports.CompileDetailed(image, nested, untouched, location);
        if (status != -2 || location[0] != 0 || location[1] != nested || location[2] < 0 ||
            untouched.Any(value => value != 0xa5))
            throw new InvalidOperationException("Nested arithmetic catches should report unsupported EH");
        cases.Add(new
        {
            module = "BootstrapLibrary", method = "Compile", fixture = "nested arithmetic catches",
            inputs = new object[] { encoded, nested },
            generated = new { capacity, arguments = Array.Empty<int>(), reject = true, diagnostic = location },
            expected = status
        });

        void CheckPairs(string method, (int A, int B)[] inputs, Func<int, int, int> oracle) =>
            Check(method, inputs.Select(pair => (new[] { pair.A, pair.B }, (Func<int>)(() => oracle(pair.A, pair.B)))));

        void Check(string method, IEnumerable<(int[] Arguments, Func<int> Oracle)> fixtures)
        {
            int row = typeof(Handlers).GetMethod(method)!.MetadataToken & 0xffffff;
            var output = new byte[capacity];
            int length = CompilerExports.Compile(image, row, output);
            if (length <= 0) throw new InvalidOperationException($"{method} compiler returned {length}");
            string hex = Convert.ToHexString(output.AsSpan(0, length)).ToLowerInvariant();
            foreach (var (arguments, oracle) in fixtures)
            {
                int? expected = null;
                string? exception = null;
                string? exceptionType = null;
                try { expected = oracle(); }
                catch (ArithmeticException error) { exception = exceptionType = error.GetType().FullName; }
                catch (IndexOutOfRangeException error) { exception = error.GetType().FullName; }
                cases.Add(new
                {
                    module = "BootstrapLibrary", method = "Compile", fixture = method,
                    inputs = new object[] { encoded, row },
                    generated = new { capacity, arguments, hex, exceptionType }, expected, exception
                });
            }
        }
    }
}
