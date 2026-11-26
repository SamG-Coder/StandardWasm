#nullable enable

using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using BootstrapLibrary;
using RethrowHandlers;

internal static class RethrowCases
{
    internal static void Add(List<object> cases)
    {
        const int capacity = 8192;
        byte[] image = File.ReadAllBytes(typeof(Handlers).Assembly.Location);
        object encoded = Encode(image);
        (int A, int B)[] inputs = [(9, 3), (9, 0), (int.MaxValue, 0), (int.MinValue, -1)];
        Check(nameof(Handlers.Escape), Handlers.Escape);
        Check(nameof(Handlers.RetainOuter), Handlers.RetainOuter);
        Check(nameof(Handlers.RethrowInner), Handlers.RethrowInner);
        Check(nameof(Handlers.RethrowInsideTry), Handlers.RethrowInsideTry);
        Check(nameof(Handlers.ViaUnmatchedFrame), Handlers.ViaUnmatchedFrame);

        using var pe = new PEReader(new MemoryStream(image));
        Reject(nameof(Handlers.Plain), "rethrow in method without EH");
        Reject(nameof(Handlers.Escape), "rethrow in a try outside any handler");

        void Reject(string method, string fixture)
        {
            int row = Row(method);
            int rva = pe.GetMetadataReader().GetMethodDefinition(MetadataTokens.MethodDefinitionHandle(row)).RelativeVirtualAddress;
            var body = pe.GetMethodBody(rva);
            if (body.GetILBytes() is not [0x02, 0x03, ..])
                throw new InvalidOperationException("Expected two argument loads at the mutation point");
            int sectionIndex = pe.PEHeaders.GetContainingSectionIndex(rva);
            var section = pe.PEHeaders.SectionHeaders[sectionIndex];
            int start = section.PointerToRawData + rva - section.VirtualAddress;
            int header = (image[start] & 3) == 2 ? 1 : (image[start + 1] >> 4) * 4;
            byte[] changed = (byte[])image.Clone();
            changed[start + header] = 0xfe;
            changed[start + header + 1] = 0x1a;
            var output = Enumerable.Repeat((byte)0xa5, capacity).ToArray();
            var diagnostic = CompilerExports.CreateDiagnostic();
            int status = CompilerExports.CompileDetailed(changed, row, output, diagnostic);
            if (status != -1 || !diagnostic.SequenceEqual(new[] { 0, row, 0 }) || output.Any(value => value != 0xa5))
                throw new InvalidOperationException($"{fixture}: {status} at [{string.Join(", ", diagnostic)}]");
            cases.Add(new
            {
                module = "BootstrapLibrary", method = "Compile", fixture,
                inputs = new[] { Encode(changed), row },
                generated = new { capacity, arguments = Array.Empty<int>(), reject = true, diagnostic },
                expected = status
            });
        }

        void Check(string fixture, Func<int, int, int> oracle)
        {
            int row = Row(fixture);
            var output = new byte[capacity];
            int length = CompilerExports.Compile(image, row, output);
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
                    inputs = new[] { encoded, row },
                    generated = new { capacity, arguments = new[] { a, b }, hex, exceptionType = exception },
                    expected, exception
                });
            }
        }
    }

    private static int Row(string method) => typeof(Handlers).GetMethod(method)!.MetadataToken & 0xffffff;

    private static object Encode(byte[] image) => new { bytes = Array.ConvertAll(image, value => (int)value) };
}
