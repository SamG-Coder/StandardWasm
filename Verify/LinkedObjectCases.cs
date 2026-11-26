#nullable enable

using System.Buffers.Binary;
using BootstrapLibrary;
using LinkedObjects;
using ObjectBridge;
using ObjectLeaf;

internal static class LinkedObjectCases
{
    internal static void Add(List<object> cases)
    {
        const int capacity = 32768;
        Type[] layouts = [typeof(EntryCell), typeof(State), typeof(LeafCell)];
        if (layouts[1].MetadataToken != layouts[2].MetadataToken)
            throw new InvalidOperationException("Expected a TypeDef row collision in the dependencies");
        var firstFields = new[]
        {
            typeof(State).GetField(nameof(State.Count))!,
            typeof(LeafCell).GetField(nameof(LeafCell.Text))!
        };
        if (firstFields.Select(field => field.MetadataToken).Distinct().Count() != 1 ||
            firstFields.Select(field => field.FieldType).Distinct().Count() != 2)
            throw new InvalidOperationException("Expected the same Field row with different dependency field types");

        byte[][] images = layouts.Select(type => File.ReadAllBytes(type.Assembly.Location)).ToArray();
        byte[] bundle = BundleCases.Pack(images);
        byte[] reordered = BundleCases.Pack(images[0], images[2], images[1]);
        object encoded = Encode(bundle);

        Check(nameof(Kernels.Run), [int.MinValue, -257, -1, 0, 5, 255, int.MaxValue], Kernels.Run, true);
        Check(nameof(Kernels.Identity), [-1, 0, 1, 31], Kernels.Identity, true);
        Check(nameof(Kernels.Touch), [0, 1], Kernels.Touch, false);
        Check(nameof(Kernels.CatchTouch), [0, 1], Kernels.CatchTouch, false);

        void Check(string fixture, int[] values, Func<int, int> oracle, bool reorder)
        {
            int row = typeof(Kernels).GetMethod(fixture)!.MetadataToken & 0xffffff;
            byte[] manifest = new byte[11];
            BinaryPrimitives.WriteInt32LittleEndian(manifest, row);
            manifest[4] = 3;
            "run"u8.CopyTo(manifest.AsSpan(8));
            object encodedManifest = Encode(manifest);
            string hex = Compile(bundle, manifest);
            foreach (int value in values) Record(encoded, fixture, value);
            if (reorder)
            {
                if (Compile(reordered, manifest) != hex)
                    throw new InvalidOperationException($"Dependency order altered {fixture}");
                Record(Encode(reordered), fixture + " with reordered dependencies", values[^1]);
            }

            void Record(object input, string name, int value)
            {
                int? expected = null;
                string? exception = null;
                try { expected = oracle(value); }
                catch (NullReferenceException error) { exception = error.GetType().FullName; }
                cases.Add(new
                {
                    module = "BootstrapLibrary", method = "CompileBundle", fixture = name,
                    inputs = new[] { input, encodedManifest },
                    generated = new { capacity, bundle = true, arguments = new[] { value }, hex, exceptionType = exception },
                    expected, exception
                });
            }
        }

        string Compile(byte[] dlls, byte[] manifest)
        {
            var output = new byte[capacity];
            var diagnostic = CompilerExports.CreateDiagnostic();
            int length = CompilerExports.CompileBundleDetailed(dlls, manifest, output, diagnostic);
            if (length <= 0)
                throw new InvalidOperationException($"Linked objects returned {length} at [{string.Join(", ", diagnostic)}]");
            return Convert.ToHexString(output.AsSpan(0, length)).ToLowerInvariant();
        }
    }

    private static object Encode(byte[] bytes) => new { bytes = Array.ConvertAll(bytes, value => (int)value) };
}
