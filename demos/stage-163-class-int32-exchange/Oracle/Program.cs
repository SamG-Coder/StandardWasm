using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using ClassInt32Exchange;

if (IntPtr.Size != 4) throw new InvalidOperationException("Expected the x86 CLR.");
Func<int>[] cases = [Probe.MixedLocations, Probe.ClassAndAggregateLocations, Probe.NullLocation, Probe.NullOwner];
string runtime = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
File.WriteAllText(args[0], JsonSerializer.Serialize(new
{
    runtime = Environment.Version.ToString(),
    assembly = typeof(Probe).Assembly.Location,
    dependencies = new[] { "System.Runtime.dll", "System.Threading.dll", "System.Private.CoreLib.dll" }
        .Select(name => Path.Combine(runtime, name)),
    cases = cases.Select(run => new { name = run.Method.Name, row = run.Method.MetadataToken & 0xffffff, expected = run() })
}));
