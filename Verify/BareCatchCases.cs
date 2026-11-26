using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using BareCatch;
using BootstrapLibrary;

internal static class BareCatchCases
{
    internal static void Add(List<object> cases)
    {
        const int capacity = 8192;
        byte[] image = File.ReadAllBytes(typeof(Handlers).Assembly.Location);
        object encoded = new { bytes = Array.ConvertAll(image, value => (int)value) };
        using var pe = new PEReader(new MemoryStream(image));
        MetadataReader metadata = pe.GetMetadataReader();
        (int, int)[] division = [(9, 3), (7, 0), (int.MinValue, -1)];
        Check(nameof(Handlers.Ordered), Handlers.Ordered, division);
        Check(nameof(Handlers.Fallback), Handlers.Fallback, [.. division, (0, 1)]);
        Check(nameof(Handlers.Rethrow), Handlers.Rethrow, division);

        void Check(string method, Func<int, int, int> oracle, (int, int)[] inputs)
        {
            int row = typeof(Handlers).GetMethod(method)!.MetadataToken & 0xffffff;
            int rva = metadata.GetMethodDefinition(MetadataTokens.MethodDefinitionHandle(row)).RelativeVirtualAddress;
            if (!pe.GetMethodBody(rva).ExceptionRegions.Any(IsObjectCatch))
                throw new InvalidOperationException($"{method} is missing its System.Object catch clause");

            var output = new byte[capacity];
            int length = CompilerExports.Compile(image, row, output);
            if (length <= 0) throw new InvalidOperationException($"{method} compiler returned {length}");
            string hex = Convert.ToHexString(output.AsSpan(0, length)).ToLowerInvariant();
            foreach (var (a, b) in inputs)
                cases.Add(new
                {
                    module = "BootstrapLibrary", method = "Compile", fixture = method,
                    inputs = new[] { encoded, row },
                    generated = new { capacity, arguments = new[] { a, b }, hex },
                    expected = oracle(a, b)
                });
        }

        bool IsObjectCatch(ExceptionRegion region)
        {
            if (region.Kind != ExceptionRegionKind.Catch || region.CatchType.Kind != HandleKind.TypeReference)
                return false;
            var type = metadata.GetTypeReference((TypeReferenceHandle)region.CatchType);
            return type.ResolutionScope.Kind == HandleKind.AssemblyReference &&
                metadata.StringComparer.Equals(type.Namespace, "System") &&
                metadata.StringComparer.Equals(type.Name, "Object");
        }
    }
}
