using Mono.Cecil;
using System.Text;

if (args.Length != 3)
{
    Console.Error.WriteLine("Usage: Compiler <assembly.dll> <full export type name> <module.wat>");
    return 2;
}
try
{
    using var assembly = AssemblyDefinition.ReadAssembly(args[0]);
    var type = assembly.MainModule.GetType(args[1]) ?? throw new InvalidDataException("Export type not found.");
    File.WriteAllText(args[2], new ModuleCompiler(assembly.MainModule).Compile(type), new UTF8Encoding(false));
    return 0;
}
catch (Exception error)
{
    Console.Error.WriteLine(error.Message);
    return 1;
}
