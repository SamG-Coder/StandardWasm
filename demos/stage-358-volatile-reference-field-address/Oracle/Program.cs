using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using VolatileReferenceFields;

if (IntPtr.Size != 4) throw new InvalidOperationException("Expected the x86 CLR.");
Func<int>[] cases = [Probe.ReplacedBucketAliases, Probe.DefaultsAndNullWrites, Probe.VolatileValueAddress];
File.WriteAllText(args[0], JsonSerializer.Serialize(new
{
    runtime = Environment.Version.ToString(),
    assembly = typeof(Probe).Assembly.Location,
    cases = cases.Select(run => new
    {
        name = run.Method.Name,
        row = run.Method.MetadataToken & 0xffffff,
        expected = run(),
        rejection = run == (Func<int>)Probe.VolatileValueAddress
            ? new { status = -2, location = new[] { 0, typeof(Probe).GetMethod(nameof(Probe.NumberAt))!.MetadataToken & 0xffffff, 7 } }
            : null
    })
}));
