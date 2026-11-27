#nullable enable
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Text.Json;
using RuntimeAssemblyArrays;

if (IntPtr.Size != 4) throw new InvalidOperationException("Expected Windows x86 CLR.");
string runtimeDirectory = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
string directory = Path.GetDirectoryName(typeof(Probe).Assembly.Location)!;
string? packages = Environment.GetEnvironmentVariable("NUGET_PACKAGES");
if (string.IsNullOrEmpty(packages))
    packages = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages");
string coreLib = Environment.GetEnvironmentVariable("STANDARDWASM_MONO_CORELIB") ?? Path.Combine(packages,
    "microsoft.netcore.app.runtime.mono.browser-wasm", Environment.Version.ToString(), "runtimes", "browser-wasm", "native",
    "System.Private.CoreLib.dll");
if (!File.Exists(coreLib)) throw new FileNotFoundException("The matching stock browser Mono CoreLib is required.", coreLib);
string corePath = Path.GetFullPath(coreLib);
string[] dependencies = new[] { "netstandard", "System.Runtime", "System.Reflection", "System.Runtime.Extensions" }
    .Select(name => Path.Combine(runtimeDirectory, name + ".dll"))
    .Append(corePath).Append(Path.Combine(directory, "ForeignAssemblyType.dll")).ToArray();
using var core = new PEReader(File.OpenRead(corePath));
MetadataReader metadata = core.GetMetadataReader();
TypeDefinition loader = metadata.GetTypeDefinition(metadata.TypeDefinitions.Single(handle =>
{
    TypeDefinition type = metadata.GetTypeDefinition(handle);
    return metadata.GetString(type.Namespace) == "System.Runtime.Loader" && metadata.GetString(type.Name) == "AssemblyLoadContext";
}));
MethodDefinitionHandle enumerate = loader.GetMethods().Single(handle =>
{
    MethodDefinition method = metadata.GetMethodDefinition(handle);
    return metadata.GetString(method.Name) == "InternalGetLoadedAssemblies" &&
        (method.ImplAttributes & MethodImplAttributes.InternalCall) != 0 && method.RelativeVirtualAddress == 0;
});
int[] enumerationLocation = [Array.IndexOf(dependencies, corePath) + 1, MetadataTokens.GetRowNumber(enumerate), -1];
Func<int>[] cases = [Probe.DescriptorElements, Probe.ForeignName, Probe.CovarianceBoundary, Probe.OriginalAppDomain];
File.WriteAllText(args[0], JsonSerializer.Serialize(new
{
    runtime = Environment.Version.ToString() + "/X86",
    assembly = typeof(Probe).Assembly.Location,
    dependencies,
    cases = cases.Select(run => new
    {
        name = run.Method.Name,
        row = run.Method.MetadataToken & 0xffffff,
        expected = run(),
        rejection = run == Probe.CovarianceBoundary
            ? new { status = -1, location = new[] { 0, run.Method.MetadataToken & 0xffffff, 10 } }
            : run == Probe.OriginalAppDomain
                ? new { status = -2, location = enumerationLocation }
                : null
    })
}));
