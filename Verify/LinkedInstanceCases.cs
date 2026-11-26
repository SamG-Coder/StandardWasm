#nullable enable

using System.Buffers.Binary;
using System.Globalization;
using System.Reflection;
using BootstrapLibrary;
using ForeignCounter;
using LinkedInstances;

internal static class LinkedInstanceCases
{
    internal static void Add(List<object> cases)
    {
        const int capacity = 32768;
        byte[] bundle = BundleCases.Pack(File.ReadAllBytes(typeof(Kernels).Assembly.Location),
            File.ReadAllBytes(typeof(Counter).Assembly.Location));
        object encoded = Encode(bundle);

        Case(nameof(Kernels.Run), [(5, 3), (0, 0), (-7, -1), (int.MaxValue, 1), (int.MinValue, -1)],
            pair => [pair.Item1, pair.Item2], pair => Kernels.Run(pair.Item1, pair.Item2));
        Case<Counter?>(nameof(Kernels.ReadNullable), [null], value => [value], value => Kernels.ReadNullable(value!));
        Case(nameof(Kernels.EvaluateOnNull), [-1, 17, int.MaxValue], value => [null, value],
            value => Kernels.EvaluateOnNull(null!, value));
        Case(nameof(Kernels.ConstructWide), [int.MinValue - 1L, int.MinValue, 31L, int.MaxValue, int.MaxValue + 1L],
            value => [new { i64 = value.ToString(CultureInfo.InvariantCulture) }], Kernels.ConstructWide);

        Case(nameof(Kernels.VirtualRead), [0], _ => [], _ => Kernels.VirtualRead());

        void Case<T>(string name, T[] values, Func<T, object?[]> arguments, Func<T, int> oracle)
        {
            byte[] manifest = Manifest(typeof(Kernels).GetMethod(name)!);
            var output = new byte[capacity];
            var diagnostic = CompilerExports.CreateDiagnostic();
            int length = CompilerExports.CompileBundleDetailed(bundle, manifest, output, diagnostic);
            if (length <= 0)
                throw new InvalidOperationException($"{name} compilation returned {length} at [{string.Join(", ", diagnostic)}]");
            string hex = Convert.ToHexString(output.AsSpan(0, length)).ToLowerInvariant();
            object[] inputs = [encoded, Encode(manifest)];
            foreach (T value in values)
            {
                int? expected = null;
                string? exception = null;
                try { expected = oracle(value); }
                catch (NullReferenceException error) { exception = error.GetType().FullName; }
                cases.Add(new
                {
                    module = "BootstrapLibrary", method = "CompileBundle", fixture = "foreign instance " + name,
                    inputs, generated = new { capacity, bundle = true, arguments = arguments(value), hex, exceptionType = exception },
                    expected, exception
                });
            }
        }
    }

    private static byte[] Manifest(MethodInfo method)
    {
        byte[] manifest = new byte[11];
        BinaryPrimitives.WriteInt32LittleEndian(manifest, method.MetadataToken & 0xffffff);
        manifest[4] = 3;
        "run"u8.CopyTo(manifest.AsSpan(8));
        return manifest;
    }

    private static object Encode(byte[] bytes) => new { bytes = Array.ConvertAll(bytes, value => (int)value) };
}
