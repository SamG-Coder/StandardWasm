#nullable enable
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using AssemblyMetadataFixture;

if (IntPtr.Size != 4) throw new InvalidOperationException("Expected Windows x86 CLR.");
Func<int>[] cases = [Probe.DecodedFields, Probe.DynamicAssemblySelection, Probe.FreshInstances, Probe.NullAssembly];
string runtime = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
string core = Environment.GetEnvironmentVariable("STANDARDWASM_MONO_CORELIB") ?? typeof(object).Assembly.Location;
File.WriteAllText(args[0], JsonSerializer.Serialize(new
{
    runtime = Environment.Version.ToString(),
    assembly = typeof(Probe).Assembly.Location,
    dependencies = new[] { typeof(EmptyMetadata.Marker).Assembly.Location, Path.Combine(runtime, "netstandard.dll"),
        Path.Combine(runtime, "System.Runtime.dll"), core },
    cases = cases.Select(run => new { name = run.Method.Name, row = run.Method.MetadataToken & 0xffffff, expected = run() })
}));
