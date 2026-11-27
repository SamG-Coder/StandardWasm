using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using ComputedSpanSlices;

if (IntPtr.Size != 4) throw new InvalidOperationException("Expected the x86 CLR.");
Func<int>[] methods = [Probe.InclusiveEnd, Probe.WriteThroughSlice, Probe.FieldDestination, Probe.RangeFailure];
File.WriteAllText(args[0], JsonSerializer.Serialize(new
{
    runtime = Environment.Version.ToString(),
    assembly = typeof(Probe).Assembly.Location,
    cases = methods.Select(run => new
    {
        name = run.Method.Name,
        row = run.Method.MetadataToken & 0xffffff,
        expected = run()
    })
}));
