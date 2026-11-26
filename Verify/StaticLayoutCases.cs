using System.Buffers.Binary;
using System.Reflection.Emit;
using BootstrapLibrary;
using FieldLayoutBase;
using FieldLayoutDemo;

internal static class StaticLayoutCases
{
    internal static void Add(List<object> cases)
    {
        const int capacity = 32768;
        byte[] image = File.ReadAllBytes(typeof(Kernels).Assembly.Location);
        byte[] dependency = File.ReadAllBytes(typeof(Record).Assembly.Location);
        byte[] bundle = BundleCases.Pack(image, dependency);
        object encoded = Encode(bundle);
        byte[] output = new byte[capacity];
        var location = CompilerExports.CreateDiagnostic();
        byte[] manifest = Manifest(nameof(Kernels.Run));
        int size = CompilerExports.CompileBundleDetailed(bundle, manifest, output, location);
        if (size <= 0)
            throw new InvalidOperationException($"Instance layout returned {size} at [{string.Join(", ", location)}].");
        string hex = Convert.ToHexString(output.AsSpan(0, size)).ToLowerInvariant();
        var inputs = new[] { encoded, Encode(manifest) };
        foreach (int value in new[] { int.MinValue, -5, 0, 5, int.MaxValue })
            cases.Add(new
            {
                module = "BootstrapLibrary", method = "CompileBundle", fixture = "mixed static and instance fields",
                inputs, generated = new { capacity, bundle = true, arguments = new[] { value }, hex },
                expected = Kernels.Run(value)
            });

        Reject(nameof(Kernels.ReadLocalStatic), OpCodes.Ldsfld);
        Reject(nameof(Kernels.ReadForeignStatic), OpCodes.Ldsfld);
        Reject(nameof(Kernels.WriteForeignStatic), OpCodes.Stsfld);
        if (Kernels.ConstructInitialized(5) != 5 || Initialized.Marker != 41)
            throw new InvalidOperationException("The CLR initializer fixture did not execute.");
        Reject(nameof(Kernels.ConstructInitialized), OpCodes.Newobj);

        void Reject(string name, OpCode opcode)
        {
            var method = typeof(Kernels).GetMethod(name)!;
            int row = method.MetadataToken & 0xffffff;
            int offset = IlOffsets.Find(method.GetMethodBody()!.GetILAsByteArray()!, opcode);
            int[] expected = [0, row, offset];
            byte[] entries = Manifest(name);
            Array.Fill(output, (byte)0xa5);
            int status = CompilerExports.CompileBundleDetailed(bundle, entries, output, location);
            if (offset < 0 || status != -2 || !location.SequenceEqual(expected) || output.Any(b => b != 0xa5))
                throw new InvalidOperationException($"{name} returned {status} at [{string.Join(", ", location)}].");
            cases.Add(new
            {
                module = "BootstrapLibrary", method = "CompileBundle", fixture = name,
                inputs = new[] { encoded, Encode(entries) },
                generated = new { capacity, bundle = true, arguments = Array.Empty<int>(), reject = true, diagnostic = expected },
                expected = status
            });
        }
    }

    private static byte[] Manifest(string name)
    {
        byte[] entries = new byte[11];
        BinaryPrimitives.WriteInt32LittleEndian(entries, typeof(Kernels).GetMethod(name)!.MetadataToken & 0xffffff);
        entries[4] = 3;
        "run"u8.CopyTo(entries.AsSpan(8));
        return entries;
    }

    private static object Encode(byte[] bytes) => new { bytes = Array.ConvertAll(bytes, value => (int)value) };
}
