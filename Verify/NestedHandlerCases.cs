#nullable enable

using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using BootstrapLibrary;
using NestedHandlers;

internal static class NestedHandlerCases
{
    internal static void Add(List<object> cases)
    {
        const int capacity = 8192;
        byte[] image = File.ReadAllBytes(typeof(Handlers).Assembly.Location);
        object encoded = Encode(image);
        Check(nameof(Handlers.InnerThenOuter), [(9, 3), (9, 0), (int.MinValue, -1)], Handlers.InnerThenOuter);
        Check(nameof(Handlers.HandlerEscapesToOuter), [(9, 3), (9, 0), (int.MaxValue, 0), (int.MinValue, -1)], Handlers.HandlerEscapesToOuter);
        Check(nameof(Handlers.NestedInsideHandler), [(9, 0), (int.MaxValue, 0), (int.MaxValue, 3)], Handlers.NestedInsideHandler);
        Check(nameof(Handlers.Disjoint), [(8, 3), (8, 0), (int.MaxValue, 1), (int.MaxValue, 0), (int.MinValue, -1)], Handlers.Disjoint);
        Check(nameof(Handlers.Escapes), [(9, 3), (9, 0), (int.MinValue, -1)], Handlers.Escapes);

        int row = Row(nameof(Handlers.InnerThenOuter));
        using var pe = new PEReader(new MemoryStream(image));
        int rva = pe.GetMetadataReader().GetMethodDefinition(MetadataTokens.MethodDefinitionHandle(row)).RelativeVirtualAddress;
        var body = pe.GetMethodBody(rva);
        var sections = pe.PEHeaders.SectionHeaders;
        var section = sections[pe.PEHeaders.GetContainingSectionIndex(rva)];
        int method = rva - section.VirtualAddress + section.PointerToRawData;
        int header = (image[method + 1] >> 4) * 4;
        int table = (method + header + body.GetILBytes()!.Length + 3) & ~3;
        if (body.ExceptionRegions.Length != 2 || image[table] != 1 || image[table + 1] != 28)
            throw new InvalidOperationException("Expected a compact exception table with two nested clauses");
        var inner = body.ExceptionRegions[0];
        var outer = body.ExceptionRegions[1];
        byte[] reversed = (byte[])image.Clone();
        image.AsSpan(table + 4, 12).CopyTo(reversed.AsSpan(table + 16));
        image.AsSpan(table + 16, 12).CopyTo(reversed.AsSpan(table + 4));
        Reject("inner clause follows its enclosing try", reversed, inner.HandlerOffset);

        byte[] intersecting = (byte[])image.Clone();
        intersecting[table + 16 + 4] = checked((byte)(inner.HandlerOffset - outer.TryOffset + 1));
        Reject("inner handler crosses enclosing try boundary", intersecting, outer.HandlerOffset);

        void Reject(string fixture, byte[] changed, int offset)
        {
            var output = Enumerable.Repeat((byte)0xa5, capacity).ToArray();
            var diagnostic = CompilerExports.CreateDiagnostic();
            int status = CompilerExports.CompileDetailed(changed, row, output, diagnostic);
            if (status != -1 || !diagnostic.SequenceEqual(new[] { 0, row, offset }) ||
                output.Any(value => value != 0xa5))
                throw new InvalidOperationException($"{fixture}: {status} at [{string.Join(", ", diagnostic)}]");
            cases.Add(new
            {
                module = "BootstrapLibrary", method = "Compile", fixture,
                inputs = new[] { Encode(changed), row },
                generated = new { capacity, arguments = Array.Empty<int>(), reject = true, diagnostic },
                expected = status
            });
        }

        void Check(string fixture, (int A, int B)[] inputs, Func<int, int, int> oracle)
        {
            int methodRow = Row(fixture);
            var output = new byte[capacity];
            int length = CompilerExports.Compile(image, methodRow, output);
            if (length <= 0) throw new InvalidOperationException($"{fixture} compiler returned {length}");
            string hex = Convert.ToHexString(output.AsSpan(0, length)).ToLowerInvariant();
            foreach (var (a, b) in inputs)
            {
                int? expected = null;
                string? exception = null;
                try { expected = oracle(a, b); }
                catch (ArithmeticException error) { exception = error.GetType().FullName; }
                cases.Add(new
                {
                    module = "BootstrapLibrary", method = "Compile", fixture,
                    inputs = new[] { encoded, methodRow },
                    generated = new { capacity, arguments = new[] { a, b }, hex, exceptionType = exception },
                    expected, exception
                });
            }
        }
    }

    private static int Row(string method) => typeof(Handlers).GetMethod(method)!.MetadataToken & 0xffffff;

    private static object Encode(byte[] image) => new { bytes = Array.ConvertAll(image, value => (int)value) };
}
