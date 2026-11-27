using System.Buffers.Binary;
using System.Reflection;
using System.Text;
using BootstrapLibrary;
using ManagedFinalizationExperiment;

internal static class EmitRootProbe
{
    private delegate int CompileRoots(byte[] bundle, byte[] entries, byte[] output, int[] location);

    internal static int Write(string outputPath)
    {
        byte[] image = File.ReadAllBytes(typeof(RootProbe).Assembly.Location);
        byte[] bundle = new byte[image.Length + sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(bundle, image.Length);
        image.CopyTo(bundle, sizeof(int));
        using var manifest = new MemoryStream();
        using var entries = new BinaryWriter(manifest);
        foreach (string name in new[] { nameof(RootProbe.ObserveFrames), nameof(RootProbe.ThrowWithRoot) })
        {
            byte[] label = Encoding.UTF8.GetBytes(name);
            entries.Write(typeof(RootProbe).GetMethod(name)!.MetadataToken & 0x00ffffff);
            entries.Write(label.Length);
            entries.Write(label);
        }

        var compile = typeof(CompilerExports).Assembly.GetType("PortableCompiler.MethodCompiler", throwOnError: true)!
            .GetMethod("CompileBundleWithRootProbe", BindingFlags.Static | BindingFlags.NonPublic)!
            .CreateDelegate<CompileRoots>();
        byte[] output = new byte[1 << 20];
        int[] location = new int[3];
        int length = compile(bundle, manifest.ToArray(), output, location);
        if (length <= 0) throw new Exception($"Root probe compiler returned {length} at {string.Join(", ", location)}");
        File.WriteAllBytes(outputPath, output[..length]);

        TextWriter original = Console.Out;
        try
        {
            Console.SetOut(TextWriter.Null);
            try { RootProbe.ThrowWithRoot(11); }
            catch (NullReferenceException) { return RootProbe.ObserveFrames(11); }
            throw new Exception("The CLR root probe did not throw NullReferenceException");
        }
        finally { Console.SetOut(original); }
    }
}
