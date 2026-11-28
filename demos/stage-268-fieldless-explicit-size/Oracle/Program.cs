using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using FieldlessExplicitSize;

if (IntPtr.Size != 4) throw new InvalidOperationException("Expected Windows x86 CLR.");
string runtime = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
Func<int>[] cases =
    [Probe.ExactSizes, Probe.ManagedCopy, Probe.NominalBox, Probe.StaticField, Probe.EnclosingSize, Probe.ByteAddress];
File.WriteAllText(args[0], JsonSerializer.Serialize(new
{
    runtime = Environment.Version.ToString() + "/X86",
    assembly = typeof(Probe).Assembly.Location,
    dependencies = new[] { "netstandard", "System.Runtime", "System.Private.CoreLib" }
        .Select(name => Path.Combine(runtime, name + ".dll")),
    cases = cases.Select(run => new
    {
        name = run.Method.Name,
        row = run.Method.MetadataToken & 0xffffff,
        expected = run(),
        rejection = run == Probe.EnclosingSize || run == Probe.ByteAddress
            ? new { status = -2, location = new[] { 0, run.Method.MetadataToken & 0xffffff, run == Probe.ByteAddress ? 8 : 0 } }
            : null
    })
}));
