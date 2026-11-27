using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using GenericReferenceUnbox;

if (IntPtr.Size != 4) throw new InvalidOperationException("Expected the x86 CLR.");
Func<int>[] cases = [Probe.DerivedIdentity, Probe.InterfaceIdentity, Probe.InterfacePattern,
    Probe.NullValue, Probe.StringIdentity, Probe.ObjectIdentity, Probe.InvalidInterface, Probe.ValueTypeStillUnboxes];
File.WriteAllText(args[0], JsonSerializer.Serialize(new
{
    runtime = Environment.Version.ToString(),
    assembly = typeof(Probe).Assembly.Location,
    cases = cases.Select(run => new
    {
        name = run.Method.Name,
        row = run.Method.MetadataToken & 0xffffff,
        expected = run()
    })
}));
