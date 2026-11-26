using System.Buffers.Binary;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Loader;
using BootstrapLibrary;
using EnumArgumentDemo;
using EnumArgumentDependency;
using Cecil = Mono.Cecil;

internal static class EnumArgumentCases
{
    internal static void Add(List<object> cases)
    {
        const int capacity = 65536;
        byte[] consumer = File.ReadAllBytes(typeof(Kernels).Assembly.Location);
        byte[] dependency = File.ReadAllBytes(typeof(Frame).Assembly.Location);
        byte[] bundle = BundleCases.Pack(consumer, dependency);
        byte[] output = new byte[capacity];
        int[] location = CompilerExports.CreateDiagnostic();

        var run = typeof(Kernels).GetMethod(nameof(Kernels.Run))!;
        byte[] entries = Manifest(run);
        int length = CompilerExports.CompileBundleDetailed(bundle, entries, output, location);
        if (length < 1)
            throw new InvalidOperationException($"{run.Name} returned {length} at [{string.Join(", ", location)}].");
        string hex = Convert.ToHexString(output.AsSpan(0, length)).ToLowerInvariant();
        foreach (int value in new[] { int.MinValue, -129, -128, -1, 0, 1, 126, 127, 128, int.MaxValue })
            cases.Add(new
            {
                module = "BootstrapLibrary", method = "CompileBundle", fixture = "nominal enum default and copy",
                inputs = new[] { Encode(bundle), Encode(entries) },
                generated = new { capacity, bundle = true, arguments = new[] { value }, hex },
                expected = Kernels.Run(value)
            });

        var read = typeof(Kernels).GetMethod(nameof(Kernels.Read))!;
        int access = IlOffsets.Find(read.GetMethodBody()!.GetILAsByteArray()!, OpCodes.Ldfld);
        if (access < 0) throw new InvalidOperationException("Missing linked field access.");
        entries = Manifest(read);
        int[] diagnostic = [0, Row(read), access];

        foreach (bool usePrimitive in new[] { false, true })
        {
            string label = usePrimitive ? "underlying primitive" : "distinct enum";
            byte[] changed = ChangeArgument(consumer, usePrimitive);
            AssertClrFieldMismatch(changed, dependency);
            byte[] mismatch = BundleCases.Pack(changed, dependency);
            Array.Fill(output, (byte)0xa5);
            int status = CompilerExports.CompileBundleDetailed(mismatch, entries, output, location);
            if (status != -6 || !location.SequenceEqual(diagnostic) || output.Any(value => value != 0xa5))
                throw new InvalidOperationException($"{label} field argument returned {status} at [{string.Join(", ", location)}].");
            cases.Add(new
            {
                module = "BootstrapLibrary", method = "CompileBundle", fixture = "nominal enum " + label,
                inputs = new[] { Encode(mismatch), Encode(entries) },
                generated = new { capacity, bundle = true, arguments = Array.Empty<int>(), reject = true, diagnostic },
                expected = status
            });
        }
    }

    private static byte[] ChangeArgument(byte[] image, bool usePrimitive)
    {
        using var assembly = Cecil.AssemblyDefinition.ReadAssembly(new MemoryStream(image));
        var module = assembly.MainModule;
        var method = module.GetType(typeof(Kernels).FullName).Methods.Single(candidate =>
            candidate.Name == nameof(Kernels.Read));
        var field = (Cecil.FieldReference)method.Body.Instructions.First(instruction =>
            instruction.OpCode.Code == Cecil.Cil.Code.Ldfld).Operand;
        var type = (Cecil.GenericInstanceType)field.FieldType;
        var left = type.GenericArguments.Single();
        type.GenericArguments[0] = usePrimitive ? module.TypeSystem.SByte :
            new Cecil.TypeReference(left.Namespace, nameof(Right), module, left.Scope, true);
        using var output = new MemoryStream();
        assembly.Write(output);
        return output.ToArray();
    }

    private static void AssertClrFieldMismatch(byte[] image, byte[] dependency)
    {
        var context = new AssemblyLoadContext("Nominal enum identity oracle", isCollectible: true);
        try
        {
            var frame = context.LoadFromStream(new MemoryStream(dependency)).GetType(typeof(Frame).FullName!)!;
            var read = context.LoadFromStream(new MemoryStream(image)).GetType(typeof(Kernels).FullName!)!
                .GetMethod(nameof(Kernels.Read))!;
            read.Invoke(null, [Activator.CreateInstance(frame)]);
            throw new InvalidOperationException("The CLR resolved a linked field using a different enum or primitive argument.");
        }
        catch (TargetInvocationException error) when (error.InnerException is MissingFieldException) { }
        finally { context.Unload(); }
    }

    private static int Row(MethodInfo method) => method.MetadataToken & 0xffffff;

    private static byte[] Manifest(MethodInfo method)
    {
        byte[] entries = new byte[11];
        BinaryPrimitives.WriteInt32LittleEndian(entries, Row(method));
        entries[4] = 3;
        "run"u8.CopyTo(entries.AsSpan(8));
        return entries;
    }

    private static object Encode(byte[] bytes) => new { bytes = Array.ConvertAll(bytes, value => (int)value) };
}
