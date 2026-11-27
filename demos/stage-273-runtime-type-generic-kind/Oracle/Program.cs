using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using GenericKind;

if (IntPtr.Size != 4) throw new InvalidOperationException("Expected Windows x86 CLR.");
Func<int>[] cases = [Probe.PlainTypes, Probe.OpenDefinitions, Probe.ClosedTypes, Probe.ArraysOfGenericElements,
    Probe.DescriptorsThroughParameter, Probe.DescriptorNullReceiver, Probe.NominalVirtualOverrides];
string runtime = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
string mono = Environment.GetEnvironmentVariable("STANDARDWASM_MONO_RUNTIME");
string facadeDirectory = mono == null ? runtime : Path.Combine(mono, "lib", "net10.0");
string core = mono == null ? typeof(object).Assembly.Location : Path.Combine(mono, "native", "System.Private.CoreLib.dll");
File.WriteAllText(args[0], JsonSerializer.Serialize(new
{
    runtime = Environment.Version.ToString(),
    assembly = typeof(Probe).Assembly.Location,
    dependencies = new[] { Path.Combine(facadeDirectory, "netstandard.dll"), Path.Combine(facadeDirectory, "System.Runtime.dll"), core },
    cases = cases.Select(run => new { name = run.Method.Name, row = run.Method.MetadataToken & 0xffffff, expected = run() })
}));
