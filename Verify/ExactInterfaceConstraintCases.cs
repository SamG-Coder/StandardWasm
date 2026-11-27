using System.Buffers.Binary;
using System.Reflection;
using System.Runtime.Loader;
using System.Security;
using BootstrapLibrary;
using InterfaceConstraints;
using Cecil = Mono.Cecil;

internal static class ExactInterfaceConstraintCases
{
    internal static void Add(List<object> cases)
    {
        const int capacity = 65536;
        const string name = nameof(Probe.ClosedInterfaceMethod);
        var probe = typeof(Probe);
        using var fixture = Cecil.AssemblyDefinition.ReadAssembly(probe.Assembly.Location);
        var method = fixture.MainModule.GetType(probe.FullName).Methods.Single(candidate => candidate.Name == name);
        var call = method.Body.Instructions.Single(instruction => instruction.Operand is Cecil.GenericInstanceMethod);
        var specialization = (Cecil.GenericInstanceMethod)call.Operand;
        var boundArgument = (Cecil.GenericInstanceType)specialization.GenericArguments[0];

        // Same interface definition, different nominal argument. C# cannot emit this call.
        boundArgument.GenericArguments[0] = fixture.MainModule.TypeSystem.String;
        using var stream = new MemoryStream();
        fixture.Write(stream);
        byte[] image = stream.ToArray();
        stream.Position = 0;
        var context = new AssemblyLoadContext("Exact interface constraint oracle", isCollectible: true);
        try
        {
            var entry = context.LoadFromStream(stream).GetType(probe.FullName)!.GetMethod(name)!;
            entry.Invoke(null, null);
            throw new InvalidOperationException("The CLR accepted IItem<string> for an IItem<int> constraint.");
        }
        catch (TargetInvocationException error) when (error.InnerException is VerificationException or TypeLoadException) { }
        finally { context.Unload(); }

        int row = method.MetadataToken.RID > 0 ? (int)method.MetadataToken.RID : throw new InvalidOperationException("Missing method row.");
        byte[] entries = new byte[11];
        BinaryPrimitives.WriteInt32LittleEndian(entries, row);
        entries[4] = 3;
        "run"u8.CopyTo(entries.AsSpan(8));
        int[] expectedDiagnostic = [0, row, call.Offset];
        byte[] bundle = BundleCases.Pack(image);
        byte[] output = new byte[capacity];
        int[] diagnostic = CompilerExports.CreateDiagnostic();
        Array.Fill(output, (byte)0xa5);
        int status = CompilerExports.CompileBundleDetailed(bundle, entries, output, diagnostic);
        if (status != -2 || !diagnostic.SequenceEqual(expectedDiagnostic) || output.Any(value => value != 0xa5))
            throw new InvalidOperationException($"Different closed interface returned {status} at [{string.Join(", ", diagnostic)}].");

        cases.Add(new
        {
            module = "BootstrapLibrary", method = "CompileBundle", fixture = "different closed interface constraint",
            inputs = new[] { Encode(bundle), Encode(entries) },
            generated = new { capacity, bundle = true, arguments = Array.Empty<int>(), reject = true, diagnostic = expectedDiagnostic },
            expected = status
        });
    }

    private static object Encode(byte[] bytes) => new { bytes = Array.ConvertAll(bytes, value => (int)value) };
}
