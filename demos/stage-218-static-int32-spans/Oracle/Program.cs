using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using StaticInt32Spans;

if (IntPtr.Size != 4) throw new InvalidOperationException("Expected Windows x86 CLR.");
var probe = typeof(Probe);
string core = typeof(object).Assembly.Location;
File.WriteAllText(args[0], JsonSerializer.Serialize(new
{
    runtime = Environment.Version.ToString(),
    assembly = probe.Assembly.Location,
    dependencies = new[] { core, Path.Combine(Path.GetDirectoryName(core)!, "System.Runtime.dll") },
    cases = probe.GetMethods(BindingFlags.Public | BindingFlags.Static).Select(test => new
    {
        name = test.Name,
        row = test.MetadataToken & 0xffffff,
        expected = (int)test.Invoke(null, null)!
    }).ToArray()
}));
