#nullable enable

using System.Buffers.Binary;
using System.Reflection.Emit;
using BootstrapLibrary;
using CastControls;
using NominalCasts;

internal static class NominalCastCases
{
    internal static void Add(List<object> cases)
    {
        const int capacity = 32768;
        byte[] entry = File.ReadAllBytes(typeof(Kernels).Assembly.Location);
        byte[] controls = File.ReadAllBytes(typeof(RemoteButton).Assembly.Location);
        byte[] bundle = BundleCases.Pack(entry, controls);
        object input = Encode(bundle);

        Check(nameof(Kernels.ReadAs), OpCodes.Isinst, Kernels.ReadAs);
        Check(nameof(Kernels.ReadCast), OpCodes.Castclass, Kernels.ReadCast);
        Check(nameof(Kernels.ReadForeignAs), OpCodes.Isinst, Kernels.ReadForeignAs);
        Check(nameof(Kernels.CatchForeignCast), OpCodes.Castclass, Kernels.CatchForeignCast);
        byte[] boolean = Manifest(nameof(Kernels.IsButton));
        string booleanHex = Compile(boolean, nameof(Kernels.IsButton));
        var booleanIl = typeof(Kernels).GetMethod(nameof(Kernels.IsButton))!.GetMethodBody()!.GetILAsByteArray()!;
        if (IlOffsets.Find(booleanIl, OpCodes.Cgt_Un) < 0)
            throw new InvalidOperationException("The boolean type test must emit cgt.un.");
        foreach (int kind in new[] { 0, 1, 2, 3 })
            cases.Add(new
            {
                module = "BootstrapLibrary", method = "CompileBundle", fixture = "nominal cast boolean is",
                inputs = new[] { input, Encode(boolean) },
                generated = new { capacity, bundle = true, arguments = new[] { kind }, hex = booleanHex },
                expected = Kernels.IsButton(kind) ? 1 : 0
            });
        byte[] eventManifest = Manifest(nameof(Kernels.ReadEventSender));
        string eventHex = Compile(eventManifest, nameof(Kernels.ReadEventSender));
        foreach (int value in new[] { 7, int.MinValue, int.MaxValue })
            cases.Add(new
            {
                module = "BootstrapLibrary", method = "CompileBundle", fixture = "nominal cast event sender",
                inputs = new[] { input, Encode(eventManifest) },
                generated = new { capacity, bundle = true, arguments = new[] { value }, hex = eventHex },
                expected = Kernels.ReadEventSender(value)
            });

        var foreign = typeof(Kernels).GetMethod(nameof(Kernels.IsForeign))!;
        int offset = IlOffsets.Find(foreign.GetMethodBody()!.GetILAsByteArray()!, OpCodes.Isinst);
        int[] diagnostic = CompilerExports.CreateDiagnostic();
        byte[] manifest = Manifest(nameof(Kernels.IsForeign));
        byte[] missing = BundleCases.Pack(entry);
        var rejected = Enumerable.Repeat((byte)0xa5, capacity).ToArray();
        int status = CompilerExports.CompileBundleDetailed(missing, manifest, rejected, diagnostic);
        if (status != -4 || !diagnostic.SequenceEqual(new[] { 0, foreign.MetadataToken & 0xffffff, offset }) ||
            rejected.Any(value => value != 0xa5))
            throw new InvalidOperationException($"Missing cast target returned {status} at [{string.Join(", ", diagnostic)}]");
        cases.Add(new
        {
            module = "BootstrapLibrary", method = "CompileBundle", fixture = "nominal cast missing target",
            inputs = new[] { Encode(missing), Encode(manifest) },
            generated = new { capacity, bundle = true, arguments = Array.Empty<int>(), reject = true, diagnostic },
            expected = status
        });

        void Check(string method, OpCode operation, Func<int, int, int> oracle)
        {
            var info = typeof(Kernels).GetMethod(method)!;
            if (IlOffsets.Find(info.GetMethodBody()!.GetILAsByteArray()!, operation) < 0)
                throw new InvalidOperationException($"{method} must emit {operation}.");
            byte[] entries = Manifest(method);
            string hex = Compile(entries, method);
            var inputs = new[] { input, Encode(entries) };

            foreach (var (kind, value) in new[] { (0, 7), (1, int.MinValue), (2, 7), (3, int.MaxValue) })
            {
                int? expected = null;
                string? exception = null;
                try { expected = oracle(kind, value); }
                catch (InvalidCastException error) { exception = error.GetType().FullName; }
                cases.Add(new
                {
                    module = "BootstrapLibrary", method = "CompileBundle", fixture = "nominal cast " + method,
                    inputs, generated = new { capacity, bundle = true, arguments = new[] { kind, value }, hex, exceptionType = exception },
                    expected, exception
                });
            }
        }

        string Compile(byte[] entries, string method)
        {
            var bytes = new byte[capacity];
            var location = CompilerExports.CreateDiagnostic();
            int size = CompilerExports.CompileBundleDetailed(bundle, entries, bytes, location);
            if (size <= 0)
                throw new InvalidOperationException($"{method} returned {size} at [{string.Join(", ", location)}]");
            return Convert.ToHexString(bytes.AsSpan(0, size)).ToLowerInvariant();
        }
    }

    private static byte[] Manifest(string method)
    {
        byte[] manifest = new byte[11];
        BinaryPrimitives.WriteInt32LittleEndian(manifest, typeof(Kernels).GetMethod(method)!.MetadataToken & 0xffffff);
        manifest[4] = 3;
        "run"u8.CopyTo(manifest.AsSpan(8));
        return manifest;
    }

    private static object Encode(byte[] bytes) => new { bytes = Array.ConvertAll(bytes, value => (int)value) };
}
