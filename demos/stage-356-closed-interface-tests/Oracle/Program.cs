using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using ClosedInterfaceTests;

if (IntPtr.Size != 4) throw new InvalidOperationException("Expected Windows x86 CLR.");
string core = typeof(object).Assembly.Location;
string runtime = Path.GetDirectoryName(core);
Func<int>[] cases = [Probe.CastPreservesIdentity, Probe.CallsOriginalImplementation,
    Probe.IsInstPreservesIdentity, Probe.RejectsDifferentConstruction, Probe.NullIsPreserved,
    Probe.ThrowsManagedInvalidCast, Probe.PrimitiveBox, Probe.RuntimeString];
File.WriteAllText(args[0], JsonSerializer.Serialize(new
{
    runtime = Environment.Version.ToString(),
    assembly = typeof(Probe).Assembly.Location,
    dependencies = new[] { Path.Combine(runtime, "netstandard.dll"), Path.Combine(runtime, "System.Runtime.dll"),
        Path.Combine(runtime, "System.Collections.dll"), core },
    cases = cases.Select(run => new
    {
        name = run.Method.Name,
        row = run.Method.MetadataToken & 0xffffff,
        expected = run()
    })
}));
