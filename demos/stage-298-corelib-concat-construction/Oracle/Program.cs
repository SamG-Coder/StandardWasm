using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using CoreLibConcatConstruction;

if (IntPtr.Size != 4) throw new InvalidOperationException("Expected Windows x86 CLR.");
Func<int>[] cases = [Probe.ThreeRawCodeUnitCopies, Probe.FourSeparateOffsets, Probe.SoleSurvivorIdentity,
    Probe.CanonicalEmpty, Probe.FreshResultIdentity, Probe.RepeatedInputAndFallback];
string runtime = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
string packages = Environment.GetEnvironmentVariable("NUGET_PACKAGES") ??
    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages");
string? mono = Environment.GetEnvironmentVariable("STAGE298_CORECLR") == "1" ? null :
    Environment.GetEnvironmentVariable("STANDARDWASM_MONO_RUNTIME") ??
    Path.Combine(packages, "microsoft.netcore.app.runtime.mono.browser-wasm", Environment.Version.ToString(),
        "runtimes", "browser-wasm");
string facades = mono == null ? runtime : Path.Combine(mono, "lib", "net10.0");
string core = mono == null ? typeof(object).Assembly.Location : Path.Combine(mono, "native", "System.Private.CoreLib.dll");
if (!File.Exists(core))
    throw new FileNotFoundException("Install the matching Mono browser-wasm runtime pack or set STANDARDWASM_MONO_RUNTIME.", core);
File.WriteAllText(args[0], JsonSerializer.Serialize(new
{
    runtime = Environment.Version.ToString(),
    assembly = typeof(Probe).Assembly.Location,
    dependencies = new[] { Path.Combine(facades, "netstandard.dll"), Path.Combine(facades, "System.Runtime.dll"), core },
    cases = cases.Select(run => new { name = run.Method.Name, row = run.Method.MetadataToken & 0xffffff, expected = run() })
}));
