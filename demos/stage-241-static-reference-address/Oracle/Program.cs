using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using ClosedStatics;

if (IntPtr.Size != 4) throw new InvalidOperationException("Expected Windows x86 CLR.");
string runtime = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
Func<int>[] cases = [Probe.Independent, Probe.MixedRepresentations, Probe.Atomic, Probe.ScalarAddressDiagnostic];
File.WriteAllText(args[0], JsonSerializer.Serialize(new
{
    runtime = Environment.Version.ToString(),
    assembly = typeof(Probe).Assembly.Location,
    dependencies = new[] { Path.Combine(runtime, "netstandard.dll"), Path.Combine(runtime, "System.Runtime.dll"),
        Path.Combine(runtime, "System.Threading.dll"), typeof(object).Assembly.Location },
    cases = cases.Select(run =>
    {
        int row = run.Method.MetadataToken & 0xffffff;
        return new
        {
            name = run.Method.Name, row, expected = run(),
            rejection = run.Method.Name == nameof(Probe.ScalarAddressDiagnostic) ? new { status = -2, location = new[] { 0, row, 0 } } : null
        };
    })
}));
