#nullable enable
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using BooleanClassByrefs;

if (IntPtr.Size != 4) throw new InvalidOperationException("Expected Windows x86 CLR.");
string runtimeDirectory = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
string[] dependencies = new[] { "netstandard", "System.Runtime", "System.Private.CoreLib" }
    .Select(name => Path.Combine(runtimeDirectory, name + ".dll")).ToArray();
Func<int>[] cases = [Probe.ClassAlias, Probe.ClassAndLocal, Probe.NullReceiver, Probe.PackedByteBoundary];
File.WriteAllText(args[0], JsonSerializer.Serialize(new
{
    runtime = Environment.Version.ToString() + "/X86",
    assembly = typeof(Probe).Assembly.Location,
    dependencies,
    cases = cases.Select(run => new
    {
        name = run.Method.Name,
        row = run.Method.MetadataToken & 0xffffff,
        expected = run(),
        rejection = run == Probe.PackedByteBoundary
            ? new { status = -2, location = new[] { 0, run.Method.MetadataToken & 0xffffff, 6 } }
            : null
    })
}));
