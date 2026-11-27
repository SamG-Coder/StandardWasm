using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using InlineReferenceArrayConstructors;

if (IntPtr.Size != 4) throw new InvalidOperationException("Expected the x86 CLR.");
Func<int>[] cases = [Probe.ConstructorSlots, Probe.DefaultAndNull, Probe.CopyAfterConstruction,
    Probe.ClosedGenericSlots, Probe.UnprovenStore];
int rejected = typeof(EffectfulSlots).GetConstructor([typeof(object)])!.MetadataToken & 0xffffff;
File.WriteAllText(args[0], JsonSerializer.Serialize(new
{
    runtime = Environment.Version.ToString(),
    assembly = typeof(Probe).Assembly.Location,
    cases = cases.Select(run => new
    {
        name = run.Method.Name,
        row = run.Method.MetadataToken & 0xffffff,
        expected = run(),
        rejection = run.Method.Name == nameof(Probe.UnprovenStore) ?
            new { status = -2, location = new[] { 0, rejected, 7 } } : null
    })
}));
