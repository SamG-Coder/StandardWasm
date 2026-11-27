using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using CharacterSpanFieldCopy;

if (IntPtr.Size != 4) throw new InvalidOperationException("Expected the x86 CLR.");
Func<int>[] methods = [Probe.Overlap, Probe.Bounded, Probe.InvalidStart, Probe.ShortDestination,
    Probe.ArrayDestination, Probe.NullArray, Probe.WritableSource];
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
