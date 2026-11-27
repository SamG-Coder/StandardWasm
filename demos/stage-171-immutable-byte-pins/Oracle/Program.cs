using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using ImmutableBytePins;

if (IntPtr.Size != 4) throw new InvalidOperationException("Expected the x86 CLR.");
Func<int>[] methods = [Probe.First, Probe.LastAndTerminator, Probe.SeparateConsumer];
string runtime = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
File.WriteAllText(args[0], JsonSerializer.Serialize(new
{
    runtime = Environment.Version.ToString(),
    assembly = typeof(Probe).Assembly.Location,
    dependencies = new[] { "System.Runtime.dll", "System.Memory.dll", "System.Private.CoreLib.dll" }
        .Select(name => Path.Combine(runtime, name)),
    cases = methods.Select(run =>
    {
        int row = run.Method.MetadataToken & 0xffffff;
        bool separate = run.Method.Name == nameof(Probe.SeparateConsumer);
        int address = Array.LastIndexOf(run.Method.GetMethodBody()!.GetILAsByteArray()!, (byte)0x12);
        return new { name = run.Method.Name, row, memory = !separate, expected = run(),
            rejection = separate ? new { status = -2, location = new[] { 0, row, address } } : null };
    })
}));
