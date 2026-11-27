using System.Text.Json;
using LocalStaticInterface;
using LocalStaticInterface.Contracts;

if (IntPtr.Size != 4) throw new InvalidOperationException("Expected Windows x86 CLR.");
string runtime = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
Func<int>[] probes = [Probe.FirstExplicitSlot, Probe.SecondExplicitSlot, Probe.ReadonlyRefTracksStore, Probe.ExternalReadonlyRefTracksStore];
File.WriteAllText(args[0], JsonSerializer.Serialize(new
{
    runtime = Environment.Version.ToString(),
    assembly = typeof(Probe).Assembly.Location,
    dependencies = new[] { typeof(ExternalLocation).Assembly.Location, Path.Combine(runtime, "System.Runtime.dll"), typeof(object).Assembly.Location },
    cases = probes.Select(run => new
    {
        name = run.Method.Name,
        row = run.Method.MetadataToken & 0xffffff,
        expected = run()
    })
}));
