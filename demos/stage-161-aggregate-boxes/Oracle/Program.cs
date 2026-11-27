using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using AggregateBoxes;

Func<int>[] cases =
[
    Probe.CachedCopy,
    Probe.DistinctBoxes,
    Probe.DefaultBox,
    Probe.RejectSameShape,
    Probe.RejectNull,
    Probe.RejectPrimitiveBox,
    Probe.NominalBoxType,
    Probe.ReferenceFieldCopy,
    Probe.BoxedInterfaceIdentity
];

File.WriteAllText(args[0], JsonSerializer.Serialize(new
{
    runtime = Environment.Version.ToString(),
    assembly = typeof(Probe).Assembly.Location,
    cases = cases.Select(run => new { name = run.Method.Name, row = run.Method.MetadataToken & 0xffffff, expected = run() })
}));
