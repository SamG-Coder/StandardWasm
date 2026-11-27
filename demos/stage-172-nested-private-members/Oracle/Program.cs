using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using NestedPrivateMembers;

if (IntPtr.Size != 4) throw new InvalidOperationException("Expected the x86 CLR.");
Func<int>[] methods = [Probe.ReadField, Probe.WriteField, Probe.CallMethod];
File.WriteAllText(args[0], JsonSerializer.Serialize(new
{
    runtime = Environment.Version.ToString(),
    assembly = typeof(Probe).Assembly.Location,
    dependencies = Array.Empty<string>(),
    cases = methods.Select(run => new
    {
        name = run.Method.Name,
        row = run.Method.MetadataToken & 0xffffff,
        expected = run()
    })
}));
