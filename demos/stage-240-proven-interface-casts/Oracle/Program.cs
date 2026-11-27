using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using ProvenInterfaceCasts;

if (IntPtr.Size != 4) throw new InvalidOperationException("Expected Windows x86 CLR.");
string runtime = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
int unknown = typeof(Probe).GetMethod("CastUnknown", BindingFlags.NonPublic | BindingFlags.Static)!.MetadataToken & 0xffffff;
Func<int>[] cases = [Probe.Identity, Probe.Null, Probe.UnknownDiagnostic];
File.WriteAllText(args[0], JsonSerializer.Serialize(new
{
    runtime = Environment.Version.ToString(),
    assembly = typeof(Probe).Assembly.Location,
    dependencies = new[] { Path.Combine(runtime, "netstandard.dll"), Path.Combine(runtime, "System.Runtime.dll"),
        Path.Combine(runtime, "System.Collections.dll"), typeof(object).Assembly.Location },
    cases = cases.Select(run => new
    {
        name = run.Method.Name, row = run.Method.MetadataToken & 0xffffff, expected = run(),
        rejection = run.Method.Name == nameof(Probe.UnknownDiagnostic) ? new { status = -2, location = new[] { 0, unknown, 1 } } : null
    })
}));
