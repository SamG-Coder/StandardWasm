#nullable enable
using System;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Text.Json;
using MonoCurrentThread;

if (IntPtr.Size != 4) throw new InvalidOperationException("Expected Windows x86 CLR.");
string runtimeDirectory = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
string? configured = Environment.GetEnvironmentVariable("STANDARDWASM_MONO_CORELIB");
string? packages = Environment.GetEnvironmentVariable("NUGET_PACKAGES");
if (string.IsNullOrEmpty(packages))
    packages = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages");
string coreLib = configured ?? Path.Combine(packages, "microsoft.netcore.app.runtime.mono.browser-wasm",
    Environment.Version.ToString(), "runtimes", "browser-wasm", "native", "System.Private.CoreLib.dll");
if (!File.Exists(coreLib)) throw new FileNotFoundException("The matching stock browser Mono CoreLib is required.", coreLib);
string[] dependencies = new[] { "netstandard", "System.Runtime", "System.Threading", "System.Threading.Thread" }
    .Select(name => Path.Combine(runtimeDirectory, name + ".dll")).Append(Path.GetFullPath(coreLib)).ToArray();
using var pe = new PEReader(File.OpenRead(coreLib));
var metadata = pe.GetMetadataReader();
var thread = metadata.TypeDefinitions.Select(metadata.GetTypeDefinition).Single(definition =>
    metadata.GetString(definition.Namespace) == "System.Threading" && metadata.GetString(definition.Name) == "Thread");
int stateMethod = MetadataTokens.GetRowNumber(thread.GetMethods().Single(method =>
    metadata.GetString(metadata.GetMethodDefinition(method).Name) == "GetState"));
Func<int>[] cases = [Probe.Identity, Probe.ManagedId, Probe.CurrentContexts, Probe.PriorityRequiresNativeState];
File.WriteAllText(args[0], JsonSerializer.Serialize(new
{
    runtime = Environment.Version.ToString(),
    assembly = typeof(Probe).Assembly.Location,
    dependencies,
    cases = cases.Select(run => new
    {
        name = run.Method.Name, row = run.Method.MetadataToken & 0xffffff, expected = run(),
        rejection = run.Method.Name == nameof(Probe.PriorityRequiresNativeState)
            ? new { status = -2, location = new[] { dependencies.Length, stateMethod, -1 } } : null
    })
}));
