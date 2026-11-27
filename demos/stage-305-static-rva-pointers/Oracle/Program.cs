using System.Reflection;
using System.Text.Json;
using StaticRvaPointersFixture;

if (IntPtr.Size != 4) throw new InvalidOperationException("Expected Windows x86 CLR.");
int Row(MethodInfo method) => method.MetadataToken & 0xffffff;
string runtime = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
Func<int>[] probes = [Probe.OriginalBytes, Probe.StableAndDistinctAddresses, Probe.ManagedAddress];
int address = Row(typeof(Probe).GetMethod("Address", BindingFlags.Static | BindingFlags.NonPublic)!);
File.WriteAllText(args[0], JsonSerializer.Serialize(new
{
    runtime = Environment.Version.ToString(),
    assembly = typeof(Probe).Assembly.Location,
    dependencies = new[] { Path.Combine(runtime, "System.Runtime.dll"), Path.Combine(runtime, "System.Memory.dll"), typeof(object).Assembly.Location },
    cases = probes.Select(run => new
    {
        name = run.Method.Name,
        row = Row(run.Method),
        expected = run(),
        memory = true,
        rejection = run.Method.Name == nameof(Probe.ManagedAddress)
            ? new { status = -2, location = new[] { 0, address, 16 } } : null
    })
}));
