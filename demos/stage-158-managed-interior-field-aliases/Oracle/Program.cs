using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using InteriorWitness;

Func<int>[] cases =
[
    Probe.ScalarAlias,
    Probe.ReferenceAlias,
    Probe.GenericCallAndArrayAlias,
    Probe.GenericCallAndClassAlias,
    Probe.NestedAggregateAlias,
    Probe.AggregateReferenceAlias,
    Probe.DescriptorShapedSnapshot
];

File.WriteAllText(args[0], JsonSerializer.Serialize(new
{
    runtime = Environment.Version.ToString(),
    assembly = typeof(Probe).Assembly.Location,
    cases = cases.Select(run => new { name = run.Method.Name, row = run.Method.MetadataToken & 0xffffff, expected = run() })
}));
