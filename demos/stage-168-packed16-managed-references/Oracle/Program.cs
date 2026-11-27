using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Packed16References;

if (IntPtr.Size != 4) throw new InvalidOperationException("Expected the x86 CLR.");
Func<int>[] methods = [Probe.SignedReadAndWrite, Probe.ReverseView, Probe.NativeBacked, Probe.NullAlias];
string runtime = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
File.WriteAllText(args[0], JsonSerializer.Serialize(new
{
    runtime = Environment.Version.ToString(),
    assembly = typeof(Probe).Assembly.Location,
    dependencies = new[] { "System.Runtime.dll", "System.Runtime.InteropServices.dll",
        "System.Runtime.CompilerServices.Unsafe.dll", "System.Private.CoreLib.dll" }
        .Select(name => Path.Combine(runtime, name)),
    cases = methods.Select(run => new { name = run.Method.Name, row = run.Method.MetadataToken & 0xffffff,
        memory = run.Method.Name == nameof(Probe.NativeBacked), expected = run() })
}));
