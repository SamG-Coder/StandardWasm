#nullable enable
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using AssemblyMetadataFixture;

if (IntPtr.Size != 4) throw new InvalidOperationException("Expected Windows x86 CLR.");
Func<int>[] cases = [Probe.DecodedFields, Probe.DynamicAssemblySelection, Probe.FreshInstances,
    Probe.EmptyArrayIdentity, Probe.NullAssembly];
string runtime = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
string? mono = Environment.GetEnvironmentVariable("STANDARDWASM_MONO_CORELIB");
string core = mono ?? typeof(object).Assembly.Location;
File.WriteAllText(args[0], JsonSerializer.Serialize(new
{
    runtime = Environment.Version.ToString(),
    assembly = typeof(Probe).Assembly.Location,
    dependencies = new[] { typeof(EmptyMetadata.Marker).Assembly.Location, Path.Combine(runtime, "netstandard.dll"),
        Path.Combine(runtime, "System.Runtime.dll"), core },
    cases = cases.Select(run => new
    {
        name = run.Method.Name,
        row = run.Method.MetadataToken & 0xffffff,
        // The genuine browser Mono 10.0.10 runtime creates a fresh typed
        // array for each empty query. The Windows CLR oracle caches it.
        expected = mono != null && run.Method.Name == nameof(Probe.EmptyArrayIdentity) ? 1157 : run()
    })
}));
