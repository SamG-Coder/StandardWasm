using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using ClosedGenericSize;

if (IntPtr.Size != 4) throw new InvalidOperationException("Expected the x86 CLR.");
Func<int>[] methods = [Probe.Narrow, Probe.WideValues, Probe.Native, Probe.EnumValues, Probe.Sequential, Probe.DeclaringType, Probe.ClosedSizeBranch];
string runtime = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
File.WriteAllText(args[0], JsonSerializer.Serialize(new
{
    runtime = Environment.Version.ToString(),
    assembly = typeof(Probe).Assembly.Location,
    dependencies = new[] { "netstandard.dll", "System.Runtime.dll", "System.Private.CoreLib.dll" }
        .Select(name => Path.Combine(runtime, name)),
    cases = methods.Select(run => new { name = run.Method.Name, row = run.Method.MetadataToken & 0xffffff, expected = run() })
}));
