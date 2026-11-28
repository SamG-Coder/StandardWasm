using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using CoreTypeBranches;

if (IntPtr.Size != 4) throw new InvalidOperationException("Expected Windows x86 CLR.");
Func<int>[] cases = [Probe.StringEquality, Probe.ReferenceEquality, Probe.StringInequality,
    Probe.ReferenceInequality, Probe.SameClosedArguments, Probe.DifferentClosedArguments, Probe.ValueKeyEquality];
File.WriteAllText(args[0], JsonSerializer.Serialize(new
{
    runtime = Environment.Version.ToString(),
    assembly = typeof(Probe).Assembly.Location,
    dependencies = new[] { typeof(object).Assembly.Location },
    cases = cases.Select(run => new
    {
        name = run.Method.Name,
        row = run.Method.MetadataToken & 0xffffff,
        expected = run()
    })
}));
