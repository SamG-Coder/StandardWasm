using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using CoreTypeComparisonFixture;

if (IntPtr.Size != 4) throw new InvalidOperationException("Expected Windows x86 CLR.");
Func<int>[] cases = [Probe.MatchingRuntimeType, Probe.DifferentRuntimeType, Probe.NullRuntimeType,
    Probe.MatchingInequality, Probe.DifferentInequality, Probe.NullInequality];
string core = typeof(object).Assembly.Location;
string runtime = Path.GetDirectoryName(core);
File.WriteAllText(args[0], JsonSerializer.Serialize(new
{
    runtime = Environment.Version.ToString(),
    assembly = typeof(Probe).Assembly.Location,
    dependencies = new[] { Path.Combine(runtime, "netstandard.dll"), Path.Combine(runtime, "System.Runtime.dll"), core },
    cases = cases.Select(run => new
    {
        name = run.Method.Name,
        row = run.Method.MetadataToken & 0xffffff,
        expected = run()
    })
}));
