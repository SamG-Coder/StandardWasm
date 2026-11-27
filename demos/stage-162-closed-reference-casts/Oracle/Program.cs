using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using ClosedCasts;

Func<int>[] cases =
[
    Probe.CastClassMatches,
    Probe.CastInterfaceMatches,
    Probe.TestClosedIdentity,
    Probe.CastNull,
    Probe.WrongClosedCast,
    Probe.ClosedParameterFromReceiver,
    Probe.ClosedParameterFromMethod
];

File.WriteAllText(args[0], JsonSerializer.Serialize(new
{
    runtime = Environment.Version.ToString(),
    assembly = typeof(Probe).Assembly.Location,
    cases = cases.Select(run => new { name = run.Method.Name, row = run.Method.MetadataToken & 0xffffff, expected = run() })
}));
