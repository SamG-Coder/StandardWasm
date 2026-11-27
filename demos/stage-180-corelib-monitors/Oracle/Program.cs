using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using CoreLibMonitors;

if (IntPtr.Size != 4) throw new InvalidOperationException("Expected the x86 CLR.");
Func<int>[] methods = [Probe.Nested, Probe.AlreadyTrue, Probe.NullGate, Probe.OriginalSetting];
string runtime = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
File.WriteAllText(args[0], JsonSerializer.Serialize(new
{
    runtime = Environment.Version.ToString(),
    assembly = typeof(Probe).Assembly.Location,
    dependencies = new[] { "System.Private.CoreLib.dll", "netstandard.dll", "System.Runtime.dll" }
        .Select(name => Path.Combine(runtime, name)),
    cases = methods.Select(run => new
    {
        name = run.Method.Name,
        row = run.Method.MetadataToken & 0xffffff,
        expected = run(),
        monitor = true
    })
}));
