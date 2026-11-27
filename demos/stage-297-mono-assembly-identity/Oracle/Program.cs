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
var marshal = metadata.TypeDefinitions.Select(metadata.GetTypeDefinition).Single(type =>
    metadata.GetString(type.Namespace) == "System.Runtime.InteropServices" && metadata.GetString(type.Name) == "Marshal");
int marshalFrontier = MetadataTokens.GetRowNumber(marshal.GetMethods().Single(handle =>
{
    var method = metadata.GetMethodDefinition(handle);
    return metadata.GetString(method.Name) == "CopyToManaged" &&
        metadata.GetBlobBytes(method.Signature).SequenceEqual(new byte[] { 0x10, 1, 4, 1, 0x18, 0x1d, 0x1e, 0, 8, 8 });
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
        rejection = index < 3 ? new { status = -2, location = new[] { 2, marshalFrontier, 0x2e } } : null
    })
}));
