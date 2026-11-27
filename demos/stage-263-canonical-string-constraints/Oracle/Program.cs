using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Text.Json;
using Mono.Cecil;
using Mono.Cecil.Cil;

if (IntPtr.Size != 4) throw new InvalidOperationException("Use the Windows x86 CLR.");
string original = typeof(Guest).Assembly.Location;
string wrongOwner = Path.Combine(Path.GetDirectoryName(original)!, "StringConstraintLookalike.dll");
string packaged = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(args[0]))!, "StringConstraintCases.dll");
string[] negatives = [nameof(Guest.WrongArgument), nameof(Guest.WrongOwner)];
using (var assembly = AssemblyDefinition.ReadAssembly(original))
{
    var guest = assembly.MainModule.GetType(nameof(Guest));
    foreach (string name in negatives)
    {
        var method = guest.Methods.Single(candidate => candidate.Name == name);
        var token = method.Body.Instructions.Single(instruction => instruction.OpCode == OpCodes.Ldtoken);
        if (token.Offset != 0 || token.Operand is not GenericInstanceType closed)
            throw new InvalidOperationException("Expected a closed generic ldtoken at entry.");
        closed.GenericArguments[0] = assembly.MainModule.TypeSystem.String;
    }
    assembly.Write(packaged);
}

var context = new AssemblyLoadContext("Closed string constraint oracle", isCollectible: true);
context.LoadFromAssemblyPath(wrongOwner);
var fixture = context.LoadFromAssemblyPath(packaged).GetType(nameof(Guest), throwOnError: true)!;
string[] names = [nameof(Guest.ClosedStringToken), nameof(Guest.DifferentCoreInterface),
    nameof(Guest.NestedSubstitution), .. negatives];
var cases = names.Select(name =>
{
    MethodInfo method = fixture.GetMethod(name)!;
    int row = method.MetadataToken & 0xffffff;
    bool reject = negatives.Contains(name);
    int expected = 0;
    try
    {
        expected = (int)method.Invoke(null, null)!;
        if (reject) throw new InvalidOperationException("The CLR accepted " + name + ".");
        int originalValue = (int)typeof(Guest).GetMethod(name)!.Invoke(null, null)!;
        if (originalValue != expected) throw new InvalidOperationException("The control changed " + name + ".");
    }
    catch (TargetInvocationException error) when (reject && error.InnerException is TypeLoadException) { }
    var rejection = reject ? new { status = -2, location = new[] { 0, row, 0 } } : null;
    return new { name, row, expected, rejection };
}).ToArray();
string coreLib = typeof(object).Assembly.Location;
string runtime = Path.Combine(Path.GetDirectoryName(coreLib)!, "System.Runtime.dll");
File.WriteAllText(args[0], JsonSerializer.Serialize(new
{
    runtime = Environment.Version + "/" + RuntimeInformation.ProcessArchitecture,
    assembly = packaged,
    dependencies = new[] { coreLib, runtime, wrongOwner },
    cases
}));
