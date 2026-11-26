using System.Buffers.Binary;
using System.Reflection;
using System.Runtime.Loader;
using BootstrapLibrary;
using HandleConstraintDemo;
using HandleConstraintDependency;
using Cecil = Mono.Cecil;

internal static class HandleConstraintCases
{
    internal static void Add(List<object> cases)
    {
        const int capacity = 65536;
        byte[] app = File.ReadAllBytes(typeof(Kernels).Assembly.Location);
        byte[] dependency = File.ReadAllBytes(typeof(Handle).Assembly.Location);
        byte[] output = new byte[capacity];
        int[] location = CompilerExports.CreateDiagnostic();

        string run = nameof(Kernels.Run), read = nameof(Kernels.Read);
        byte[] runEntries = Manifest(run);
        byte[] bundle = BundleCases.Pack(app, dependency);
        string runHex = Compile(bundle, runEntries, run);
        foreach (int value in new[] { int.MinValue, -17, 0, 17, int.MaxValue })
            Accept(bundle, runEntries, [value], Kernels.Run(value), runHex, "closed handle copy");

        byte[] inherited = RemoveDirectInterface(dependency);
        byte[] inheritedBundle = BundleCases.Pack(app, inherited);
        int inheritedResult = InvokeClr(app, inherited, run, [-123]);
        Accept(inheritedBundle, runEntries, [-123], inheritedResult,
            Compile(inheritedBundle, runEntries, run), "transitive interface");

        byte[] entries = Manifest(read);
        byte[] referenceArgument = ChangeArgument(app, typeof(ReferenceHandle));
        int accepted = InvokeClr(referenceArgument, dependency, read, [null]);
        byte[] referenceBundle = BundleCases.Pack(referenceArgument, dependency);
        Accept(referenceBundle, entries, [null], accepted, Compile(referenceBundle, entries, read), "compiled unmanaged marker");

        foreach (Type wrong in new[] { typeof(DifferentArgument), typeof(DifferentDefinition) })
        {
            byte[] changed = ChangeArgument(app, wrong);
            try
            {
                InvokeClr(changed, dependency, read, [null]);
                throw new InvalidOperationException("The CLR accepted " + wrong.Name + ".");
            }
            catch (TypeLoadException) { }
            catch (TargetInvocationException error) when (error.InnerException is TypeLoadException) { }

            byte[] rejected = BundleCases.Pack(changed, dependency);
            int[] diagnostic = [0, Row(read), -1];
            Array.Fill(output, (byte)0xa5);
            int status = CompilerExports.CompileBundleDetailed(rejected, entries, output, location);
            if (status != -2 || !location.SequenceEqual(diagnostic) || output.Any(value => value != 0xa5))
                throw new InvalidOperationException($"{wrong.Name} returned {status} at [{string.Join(", ", location)}].");
            cases.Add(new
            {
                module = "BootstrapLibrary", method = "CompileBundle", fixture = "handle constraint " + wrong.Name,
                inputs = new[] { Encode(rejected), Encode(entries) },
                generated = new { capacity, bundle = true, arguments = Array.Empty<int>(), reject = true, diagnostic },
                expected = status
            });
        }

        string Compile(byte[] input, byte[] manifest, string name)
        {
            int length = CompilerExports.CompileBundleDetailed(input, manifest, output, location);
            if (length < 1)
                throw new InvalidOperationException($"{name} returned {length} at [{string.Join(", ", location)}].");
            return Convert.ToHexString(output.AsSpan(0, length)).ToLowerInvariant();
        }

        void Accept(byte[] input, byte[] manifest, object[] arguments, int expected, string hex, string label) =>
            cases.Add(new
            {
                module = "BootstrapLibrary", method = "CompileBundle", fixture = "handle constraint " + label,
                inputs = new[] { Encode(input), Encode(manifest) },
                generated = new { capacity, bundle = true, arguments, hex },
                expected
            });
    }

    private static byte[] RemoveDirectInterface(byte[] image)
    {
        using var assembly = Cecil.AssemblyDefinition.ReadAssembly(new MemoryStream(image));
        var handle = assembly.MainModule.GetType(typeof(Handle).FullName);
        var direct = handle.Interfaces.SingleOrDefault(implementation =>
            implementation.InterfaceType.GetElementType().Name == typeof(IShape<>).Name);
        if (direct != null) handle.Interfaces.Remove(direct);
        using var output = new MemoryStream();
        assembly.Write(output);
        return output.ToArray();
    }

    private static byte[] ChangeArgument(byte[] image, Type replacement)
    {
        using var assembly = Cecil.AssemblyDefinition.ReadAssembly(new MemoryStream(image));
        var module = assembly.MainModule;
        var method = module.GetType(typeof(Kernels).FullName).Methods.Single(candidate => candidate.Name == nameof(Kernels.Read));
        var closed = (Cecil.GenericInstanceType)method.Parameters[0].ParameterType;
        closed.GenericArguments[0] = module.ImportReference(replacement);
        using var output = new MemoryStream();
        assembly.Write(output);
        return output.ToArray();
    }

    private static int InvokeClr(byte[] app, byte[] dependency, string method, object[] arguments)
    {
        var context = new AssemblyLoadContext("Closed constraint oracle", isCollectible: true);
        try
        {
            context.LoadFromStream(new MemoryStream(dependency));
            var entry = context.LoadFromStream(new MemoryStream(app)).GetType(typeof(Kernels).FullName!)!.GetMethod(method)!;
            return (int)entry.Invoke(null, arguments)!;
        }
        finally { context.Unload(); }
    }

    private static int Row(string method) => typeof(Kernels).GetMethod(method)!.MetadataToken & 0xffffff;

    private static byte[] Manifest(string method)
    {
        byte[] entries = new byte[11];
        BinaryPrimitives.WriteInt32LittleEndian(entries, Row(method));
        entries[4] = 3;
        "run"u8.CopyTo(entries.AsSpan(8));
        return entries;
    }

    private static object Encode(byte[] bytes) => new { bytes = Array.ConvertAll(bytes, value => (int)value) };
}
