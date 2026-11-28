#nullable enable
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using BooleanStorageAliases;

if (IntPtr.Size != 4) throw new InvalidOperationException("Expected Windows x86 CLR.");
string runtimeDirectory = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
string? packages = Environment.GetEnvironmentVariable("NUGET_PACKAGES");
if (string.IsNullOrEmpty(packages))
    packages = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages");
string coreLib = Environment.GetEnvironmentVariable("STANDARDWASM_MONO_CORELIB") ?? Path.Combine(packages,
    "microsoft.netcore.app.runtime.mono.browser-wasm", Environment.Version.ToString(), "runtimes", "browser-wasm", "native",
    "System.Private.CoreLib.dll");
if (!File.Exists(coreLib)) throw new FileNotFoundException("The matching stock browser Mono CoreLib is required.", coreLib);
string[] dependencies = new[] { "netstandard", "System.Runtime", "System.Threading", "System.Threading.Thread" }
    .Select(name => Path.Combine(runtimeDirectory, name + ".dll")).Append(Path.GetFullPath(coreLib)).ToArray();
Func<int>[] cases =
    [Probe.OrdinaryWrapper, Probe.OriginalVolatileClass, Probe.OriginalVolatileLocal, Probe.TypedNull, Probe.EscapingWrapper];
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
        rejection = run == Probe.EscapingWrapper
            ? new { status = -2, location = new[] { 0, run.Method.MetadataToken & 0xffffff, 4 } }
            : null
    })
}));
