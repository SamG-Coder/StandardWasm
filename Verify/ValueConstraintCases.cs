using System.Buffers.Binary;
using System.Reflection;
using System.Runtime.Loader;
using BootstrapLibrary;
using ValueConstraintDemo;
using ValueConstraintDependency;
using Cecil = Mono.Cecil;

internal static class ValueConstraintCases
{
    internal static void Add(List<object> cases)
    {
        const int capacity = 65536;
        byte[] consumer = File.ReadAllBytes(typeof(Kernels).Assembly.Location);
        byte[] dependency = File.ReadAllBytes(typeof(Frame).Assembly.Location);
        byte[] bundle = BundleCases.Pack(consumer, dependency);
        byte[] output = new byte[capacity];
        int[] location = CompilerExports.CreateDiagnostic();

        string run = nameof(Kernels.Run);
        byte[] entries = Manifest(run);
        int length = CompilerExports.CompileBundleDetailed(bundle, entries, output, location);
        if (length < 1) throw new InvalidOperationException($"{run} returned {length} at [{string.Join(", ", location)}].");
        string hex = Convert.ToHexString(output.AsSpan(0, length)).ToLowerInvariant();
        foreach (int value in new[] { int.MinValue, -7, 0, 7, int.MaxValue })
            cases.Add(new
            {
                module = "BootstrapLibrary", method = "CompileBundle", fixture = "nonnullable constraint ordinary arguments",
                inputs = new[] { Encode(bundle), Encode(entries) },
                generated = new { capacity, bundle = true, arguments = new[] { value }, hex },
                expected = Kernels.Run(value)
            });

        string constrained = nameof(Kernels.ReadConstraint);
        byte[] badReference = EncodeReference(consumer, constrained);
        AssertClrReferenceFailure(badReference, dependency, constrained);
        Reject(BundleCases.Pack(badReference, dependency), constrained, "reference argument");

        if (Kernels.RejectStandaloneConstructor(null) != 1 || Kernels.RejectNominal(null) != 1)
            throw new InvalidOperationException("CLR did not accept the valid constraint declarations.");
        Reject(bundle, nameof(Kernels.RejectStandaloneConstructor), "standalone constructor constraint");
        Reject(bundle, nameof(Kernels.RejectNominal), "arbitrary nominal constraint");

        void Reject(byte[] bytes, string name, string label)
        {
            byte[] manifest = Manifest(name);
            int[] diagnostic = [0, Row(name), -1];
            Array.Fill(output, (byte)0xa5);
            int status = CompilerExports.CompileBundleDetailed(bytes, manifest, output, location);
            if (status != -2 || !location.SequenceEqual(diagnostic) || output.Any(value => value != 0xa5))
                throw new InvalidOperationException($"{label} returned {status} at [{string.Join(", ", location)}].");
            cases.Add(new
            {
                module = "BootstrapLibrary", method = "CompileBundle", fixture = "nonnullable constraint " + label,
                inputs = new[] { Encode(bytes), Encode(manifest) },
                generated = new { capacity, bundle = true, arguments = Array.Empty<int>(), reject = true, diagnostic },
                expected = status
            });
        }
    }

    private static byte[] EncodeReference(byte[] image, string name)
    {
        using var assembly = Cecil.AssemblyDefinition.ReadAssembly(new MemoryStream(image));
        var module = assembly.MainModule;
        var method = module.GetType(typeof(Kernels).FullName).Methods.Single(candidate => candidate.Name == name);
        var type = (Cecil.GenericInstanceType)method.Parameters[0].ParameterType;
        type.GenericArguments[0] = module.TypeSystem.String;
        using var output = new MemoryStream();
        assembly.Write(output);
        return output.ToArray();
    }

    private static void AssertClrReferenceFailure(byte[] image, byte[] dependency, string name)
    {
        var context = new AssemblyLoadContext("Nonnullable value constraint oracle", isCollectible: true);
        try
        {
            context.LoadFromStream(new MemoryStream(dependency));
            var entry = context.LoadFromStream(new MemoryStream(image)).GetType(typeof(Kernels).FullName!)!.GetMethod(name)!;
            entry.Invoke(null, [null]);
            throw new InvalidOperationException("The CLR accepted String under a non-nullable value constraint.");
        }
        catch (TypeLoadException) { }
        catch (TargetInvocationException error) when (error.InnerException is TypeLoadException) { }
        finally { context.Unload(); }
    }

    private static int Row(string name) => typeof(Kernels).GetMethod(name)!.MetadataToken & 0xffffff;

    private static byte[] Manifest(string name)
    {
        byte[] entries = new byte[11];
        BinaryPrimitives.WriteInt32LittleEndian(entries, Row(name));
        entries[4] = 3;
        "run"u8.CopyTo(entries.AsSpan(8));
        return entries;
    }

    private static object Encode(byte[] bytes) => new { bytes = Array.ConvertAll(bytes, value => (int)value) };
}
