#nullable enable

using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using BootstrapLibrary;
using FinallyLibrary;

internal static class FinallyCases
{
    internal static void Add(List<object> cases)
    {
        const int capacity = 16384;
        byte[] image = File.ReadAllBytes(typeof(Kernels).Assembly.Location);
        object encoded = Encode(image);
        Check(nameof(Kernels.NestedLeave), [[-1], [0], [1]], args => Kernels.NestedLeave(args[0]));
        Check(nameof(Kernels.CatchAfterCleanup), [[0], [1], [4], [-2]], args => Kernels.CatchAfterCleanup(args[0]));
        Check(nameof(Kernels.Replacement), [[0, 4], [1, 4], [0, int.MaxValue], [1, int.MaxValue]],
            args => Kernels.Replacement(args[0], args[1]));
        Check(nameof(Kernels.NestedInCleanup), [[0, 0], [0, 3], [4, 0], [4, 3]],
            args => Kernels.NestedInCleanup(args[0], args[1]));
        Check(nameof(Kernels.ReturnBeforeCleanup), [[-1], [0], [int.MaxValue]],
            args => Kernels.ReturnBeforeCleanup(args[0]));

        using var pe = new PEReader(new MemoryStream(image));
        Reject(nameof(Kernels.Plain), "endfinally outside exception handling", 0, OpCodes.Endfinally);
        int returnRva = Rva(Row(nameof(Kernels.ReturnBeforeCleanup)));
        int cleanupEnd = IlOffsets.Find(pe.GetMethodBody(returnRva).GetILBytes()!, OpCodes.Endfinally);
        if (cleanupEnd < 0) throw new InvalidOperationException("Expected endfinally in the CLR body");
        Reject(nameof(Kernels.ReturnBeforeCleanup), "ret exits a finally handler", cleanupEnd, OpCodes.Ret);

        int Rva(int row) =>
            pe.GetMetadataReader().GetMethodDefinition(MetadataTokens.MethodDefinitionHandle(row)).RelativeVirtualAddress;

        void Reject(string method, string fixture, int offset, OpCode replace)
        {
            int row = Row(method), rva = Rva(row);
            var section = pe.PEHeaders.SectionHeaders[pe.PEHeaders.GetContainingSectionIndex(rva)];
            int start = section.PointerToRawData + rva - section.VirtualAddress;
            int header = (image[start] & 3) == 2 ? 1 : (image[start + 1] >> 4) * 4;
            byte[] changed = (byte[])image.Clone();
            changed[start + header + offset] = unchecked((byte)replace.Value);
            var output = Enumerable.Repeat((byte)0xa5, capacity).ToArray();
            var diagnostic = CompilerExports.CreateDiagnostic();
            int status = CompilerExports.CompileDetailed(changed, row, output, diagnostic);
            if (status != -1 || !diagnostic.SequenceEqual(new[] { 0, row, offset }) || output.Any(value => value != 0xa5))
                throw new InvalidOperationException($"{fixture}: {status} at [{string.Join(", ", diagnostic)}]");
            cases.Add(new
            {
                module = "BootstrapLibrary", method = "Compile", fixture,
                inputs = new[] { Encode(changed), row },
                generated = new { capacity, arguments = Array.Empty<int>(), reject = true, diagnostic },
                expected = status
            });
        }

        void Check(string fixture, int[][] inputs, Func<int[], int> oracle)
        {
            int row = Row(fixture);
            var output = new byte[capacity];
            int length = CompilerExports.Compile(image, row, output);
            if (length <= 0) throw new InvalidOperationException($"{fixture} compiler returned {length}");
            string hex = Convert.ToHexString(output.AsSpan(0, length)).ToLowerInvariant();
            foreach (int[] arguments in inputs)
            {
                int? expected = null;
                string? exception = null;
                try { expected = oracle(arguments); }
                catch (ArithmeticException error) { exception = error.GetType().FullName; }
                cases.Add(new
                {
                    module = "BootstrapLibrary", method = "Compile", fixture,
                    inputs = new[] { encoded, row }, generated = new { capacity, arguments, hex, exceptionType = exception },
                    expected, exception
                });
            }
        }
    }

    private static int Row(string method) => typeof(Kernels).GetMethod(method)!.MetadataToken & 0xffffff;

    private static object Encode(byte[] image) => new { bytes = Array.ConvertAll(image, value => (int)value) };
}
