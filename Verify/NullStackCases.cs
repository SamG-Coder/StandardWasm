#nullable enable

using System.Buffers.Binary;
using System.Reflection;
using System.Reflection.Emit;
using BootstrapLibrary;
using NullStack;
using NullStackDependency;

internal static class NullStackCases
{
    internal static void Add(List<object> cases)
    {
        const int capacity = 32768;
        byte[] image = File.ReadAllBytes(typeof(Kernels).Assembly.Location);
        object library = Encode(image);
        byte[] bundle = BundleCases.Pack(image, File.ReadAllBytes(typeof(RemoteMeter).Assembly.Location));

        foreach (int value in new[] { int.MinValue, 0, 7 })
        {
            Check(nameof(Kernels.Read), [value], () => Kernels.Read(value));
            Check(nameof(Kernels.ReadReversed), [value], () => Kernels.ReadReversed(value));
            Check(nameof(Kernels.ReadLinked), [value], () => Kernels.ReadLinked(value), true);
        }
        foreach (int value in new[] { -1, 0, 5 })
        {
            Check(nameof(Kernels.ReadLength), [value], () => Kernels.ReadLength(value));
            Check(nameof(Kernels.ReadText), [value], () => Kernels.ReadText(value));
        }
        foreach (int value in new[] { 0, 1, 2, 3, 4, 31 })
            Check(nameof(Kernels.Loop), [value], () => Kernels.Loop(value));
        Check(nameof(Kernels.ReadNullField), [], Kernels.ReadNullField);
        Check(nameof(Kernels.ReadNullElement), [], Kernels.ReadNullElement);

        void Check(string name, int[] arguments, Func<int> oracle, bool linked = false)
        {
            MethodInfo method = typeof(Kernels).GetMethod(name)!;
            if (IlOffsets.Find(method.GetMethodBody()!.GetILAsByteArray()!, OpCodes.Ldnull) < 0)
                throw new InvalidOperationException($"{name} must put an actual ldnull on the CLI stack");
            int row = method.MetadataToken & 0xffffff;
            byte[] manifest = new byte[11];
            BinaryPrimitives.WriteInt32LittleEndian(manifest, row);
            manifest[4] = 3;
            "run"u8.CopyTo(manifest.AsSpan(8));

            var output = new byte[capacity];
            int[] location = CompilerExports.CreateDiagnostic();
            int length = linked
                ? CompilerExports.CompileBundleDetailed(bundle, manifest, output, location)
                : CompilerExports.CompileDetailed(image, row, output, location);
            if (length <= 0)
                throw new InvalidOperationException($"{name} returned {length} at [{string.Join(", ", location)}]");

            int? expected = null;
            string? exception = null;
            try { expected = oracle(); }
            catch (NullReferenceException error) { exception = error.GetType().FullName; }
            cases.Add(new
            {
                module = "BootstrapLibrary", method = linked ? "CompileBundle" : "Compile", fixture = "null stack " + name,
                inputs = linked ? new[] { Encode(bundle), Encode(manifest) } : new[] { library, row },
                generated = new { capacity, bundle = linked, arguments, hex = Convert.ToHexString(output.AsSpan(0, length)).ToLowerInvariant(),
                    exceptionType = exception },
                expected, exception
            });
        }
    }

    private static object Encode(byte[] bytes) => new { bytes = Array.ConvertAll(bytes, value => (int)value) };
}
