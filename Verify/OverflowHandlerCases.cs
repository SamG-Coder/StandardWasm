#nullable enable

using System.Buffers.Binary;
using System.Globalization;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using BootstrapLibrary;
using OverflowHandlers;

internal static class OverflowHandlerCases
{
    internal static void Add(List<object> cases)
    {
        const int capacity = 8192;
        byte[] image = File.ReadAllBytes(typeof(Handlers).Assembly.Location);
        var encoded = Encode(image);

        Check(nameof(Handlers.Increment), new[] { int.MinValue, -1, 0, int.MaxValue - 1, int.MaxValue }
            .Select(value => (new object[] { value }, (Func<object>)(() => Handlers.Increment(value)))));
        CheckPairs(nameof(Handlers.FromCall), [(0, 19), (10, -3), (int.MaxValue, 19), (12, int.MaxValue)], Handlers.FromCall);
        CheckPairs(nameof(Handlers.BeforeTry), [(4, 5), (1, int.MaxValue), (int.MaxValue, 3)], Handlers.BeforeTry);
        CheckPairs(nameof(Handlers.InsideHandler), [(1, int.MaxValue), (int.MaxValue, 10), (int.MaxValue, int.MaxValue)], Handlers.InsideHandler);

        (int Operation, long A, long B)[] division = [(0, 10, 2), (1, 98, 17), (0, long.MinValue, -1),
            (1, long.MinValue, -1), (0, 1, 0), (1, 1, 0)];
        Check(nameof(Handlers.DivideOrRemainder), division.Select(value =>
            (new[] { (object)value.Operation, Wide(value.A), Wide(value.B) },
                (Func<object>)(() => Wide(Handlers.DivideOrRemainder(value.Operation, value.A, value.B))))));

        int row = Row(nameof(Handlers.Increment));
        using var pe = new PEReader(new MemoryStream(image));
        int rva = pe.GetMetadataReader().GetMethodDefinition(MetadataTokens.MethodDefinitionHandle(row)).RelativeVirtualAddress;
        var body = pe.GetMethodBody(rva);
        int leave = IlOffsets.Find(body.GetILBytes()!, OpCodes.Leave_S);
        if (leave < 0) throw new InvalidOperationException("Increment did not emit a short leave");
        var section = pe.PEHeaders.SectionHeaders[pe.PEHeaders.GetContainingSectionIndex(rva)];
        int start = section.PointerToRawData + rva - section.VirtualAddress;
        int header = (BinaryPrimitives.ReadUInt16LittleEndian(image.AsSpan(start)) >> 12) * 4;
        byte[] wrongEdge = (byte[])image.Clone();
        wrongEdge[start + header + leave] = unchecked((byte)OpCodes.Br_S.Value);
        var rejected = Enumerable.Repeat((byte)0xa5, capacity).ToArray();
        var diagnostic = CompilerExports.CreateDiagnostic();
        int status = CompilerExports.CompileDetailed(wrongEdge, row, rejected, diagnostic);
        if (status != -1 || !diagnostic.SequenceEqual(new[] { 0, row, leave }) || rejected.Any(value => value != 0xa5))
            throw new InvalidOperationException("An ordinary branch escaped the protected region");
        cases.Add(new
        {
            module = "BootstrapLibrary", method = "Compile", fixture = "branch escapes protected region",
            inputs = new[] { Encode(wrongEdge), row },
            generated = new { capacity, arguments = Array.Empty<int>(), reject = true, diagnostic }, expected = status
        });

        void CheckPairs(string method, (int A, int B)[] inputs, Func<int, int, int> oracle) =>
            Check(method, inputs.Select(value => (new object[] { value.A, value.B }, (Func<object>)(() => oracle(value.A, value.B)))));

        void Check(string method, IEnumerable<(object[] Arguments, Func<object> Oracle)> fixtures)
        {
            int index = Row(method);
            var output = new byte[capacity];
            int length = CompilerExports.Compile(image, index, output);
            if (length <= 0) throw new InvalidOperationException($"{method} compiler returned {length}");
            string hex = Convert.ToHexString(output.AsSpan(0, length)).ToLowerInvariant();
            foreach (var (arguments, oracle) in fixtures)
            {
                object? expected;
                string? exception = null;
                string? exceptionType = null;
                try { expected = oracle(); }
                catch (ArithmeticException error)
                {
                    expected = null;
                    exception = error.GetType().FullName;
                    exceptionType = exception;
                }
                cases.Add(new
                {
                    module = "BootstrapLibrary", method = "Compile", fixture = method,
                    inputs = new[] { encoded, index }, generated = new { capacity, arguments, hex, exceptionType }, expected, exception
                });
            }
        }
    }

    private static int Row(string method) => typeof(Handlers).GetMethod(method)!.MetadataToken & 0xffffff;
    private static object Encode(byte[] bytes) => new { bytes = Array.ConvertAll(bytes, value => (int)value) };
    private static object Wide(long value) => new { i64 = value.ToString(CultureInfo.InvariantCulture) };
}
