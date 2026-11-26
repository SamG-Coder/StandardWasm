using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using BootstrapLibrary;
using SkipLocalsInitFixture;

// Stage 123 (#268): methods compiled with [SkipLocalsInit] clear CorILMethod_InitLocals.
// The compiler gives them zeroed locals (decision 0051), and stackalloc stays diagnosed.
internal static class SkipLocalsInitCases
{
    internal static void Add(List<object> cases)
    {
        const int capacity = 8192;
        byte[] image = File.ReadAllBytes(typeof(Kernels).Assembly.Location);
        object encoded = new { bytes = Array.ConvertAll(image, value => (int)value) };
        using var pe = new PEReader(new MemoryStream(image));
        MetadataReader metadata = pe.GetMetadataReader();
        int[] inputs = [int.MinValue, -7, -1, 0, 1, 5, 11, 40];

        Accept(nameof(Kernels.Scalars), Kernels.Scalars);
        Accept(nameof(Kernels.Value), Kernels.Value);
        Accept(nameof(Kernels.Nested), x => Kernels.Nested(x), [-3, 0, 1, 5, 20]);
        Accept(nameof(Kernels.Objects), Kernels.Objects);
        // Stage 137 (decision 0065): stackalloc runs on the shadow stack in memory 0.
        Accept(nameof(Kernels.Stack), Kernels.Stack, memory: true);

        void Accept(string method, Func<int, int> oracle, int[]? values = null, bool memory = false)
        {
            // Release optimizes Stack's pointer local away, so its row is taken directly.
            int row = memory ? typeof(Kernels).GetMethod(method)!.MetadataToken & 0xffffff : RowWithoutInitLocals(method);
            var output = new byte[capacity];
            int length = CompilerExports.Compile(image, row, output);
            if (length <= 0) throw new InvalidOperationException($"{method} compiler returned {length}");
            string hex = Convert.ToHexString(output.AsSpan(0, length)).ToLowerInvariant();
            foreach (int x in values ?? inputs)
                cases.Add(new
                {
                    module = "BootstrapLibrary", method = "Compile", fixture = "skip-locals-init " + method,
                    inputs = new[] { encoded, row },
                    generated = memory ? (object)new { capacity, arguments = new[] { x }, hex, memory = true }
                        : new { capacity, arguments = new[] { x }, hex },
                    expected = oracle(x)
                });
        }

        // Pin the fixture: each kernel must have locals and a cleared InitLocals flag,
        // or the cases would not exercise the rule they claim to cover.
        int RowWithoutInitLocals(string method)
        {
            int row = typeof(Kernels).GetMethod(method)!.MetadataToken & 0xffffff;
            int rva = metadata.GetMethodDefinition(MetadataTokens.MethodDefinitionHandle(row)).RelativeVirtualAddress;
            MethodBodyBlock body = pe.GetMethodBody(rva);
            if (body.LocalVariablesInitialized || body.LocalSignature.IsNil)
                throw new InvalidOperationException($"{method} must have locals without CorILMethod_InitLocals");
            return row;
        }
    }
}
