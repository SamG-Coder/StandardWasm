#nullable enable
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using System.Text.Json;

if (IntPtr.Size != 4) throw new InvalidOperationException("Use the Windows x86 CLR.");
string runtime = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
string coreLib = Environment.GetEnvironmentVariable("STANDARDWASM_MONO_CORELIB") ??
    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages",
        "microsoft.netcore.app.runtime.mono.browser-wasm", Environment.Version.ToString(), "runtimes",
        "browser-wasm", "native", "System.Private.CoreLib.dll");
using var core = new PEReader(File.OpenRead(coreLib));
MetadataReader metadata = core.GetMetadataReader();
var searchValues = metadata.TypeDefinitions.Select(metadata.GetTypeDefinition).Single(type =>
    metadata.GetString(type.Namespace) == "System.Buffers" && metadata.GetString(type.Name) == "SearchValues");
int searchFrontier = MetadataTokens.GetRowNumber(searchValues.GetMethods().Single(handle =>
{
    var method = metadata.GetMethodDefinition(handle);
    byte[] signature = metadata.GetBlobBytes(method.Signature);
    return metadata.GetString(method.Name) == "Create" && signature.Length > 6 && signature[0] == 0 &&
        signature[1] == 1 && signature[^1] == 3;
}));
Func<int>[] cases = [Probe.Default, Probe.WithFlag, Probe.Version, Probe.ManagedIdentity, Probe.CustomSlot];
File.WriteAllText(args[0], JsonSerializer.Serialize(new
{
    runtime = Environment.Version + "/" + RuntimeInformation.ProcessArchitecture,
    assembly = typeof(Probe).Assembly.Location,
    dependencies = new[] { Path.Combine(runtime, "System.Runtime.dll"), coreLib },
    cases = cases.Select((run, index) => new
    {
        name = run.Method.Name,
        row = run.Method.MetadataToken & 0xffffff,
        expected = run(),
        rejection = index < 3 ? new { status = -2, location = new[] { 2, searchFrontier, 0x35 } } : null
    })
}));
