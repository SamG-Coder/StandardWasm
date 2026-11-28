using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using TypeInstanceFields;

if (IntPtr.Size != 4) throw new InvalidOperationException("Expected Windows x86 CLR.");
Func<int>[] cases = [Probe.Identity, Probe.SeparateInstances, Probe.NullAndDefault, Probe.StructCopy, Probe.MutableDiagnostic];
string runtime = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
int mutableConstructor = typeof(MutableSlot).GetConstructors().Single().MetadataToken & 0xffffff;
File.WriteAllText(args[0], JsonSerializer.Serialize(new
{
    runtime = Environment.Version.ToString(),
    assembly = typeof(Probe).Assembly.Location,
    dependencies = new[] { Path.Combine(runtime, "netstandard.dll"), Path.Combine(runtime, "System.Runtime.dll"),
        typeof(object).Assembly.Location },
    cases = cases.Select(run => new
    {
        name = run.Method.Name,
        row = run.Method.MetadataToken & 0xffffff,
        expected = run(),
        rejection = run.Method.Name == nameof(Probe.MutableDiagnostic)
            ? new { status = -1, location = new[] { 0, mutableConstructor, 8 } } : null
    })
}));
