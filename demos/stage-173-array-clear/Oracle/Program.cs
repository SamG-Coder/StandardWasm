using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using ManagedArrayClear;

if (IntPtr.Size != 4) throw new InvalidOperationException("Expected the Windows x86 CLR.");
Func<int>[] cases = [Probe.WholeNumeric, Probe.ReferenceRange, Probe.ReferenceAlias, Probe.NetStandardPacked,
    Probe.NumericAlias, Probe.WholeAggregate,
    Probe.ProjectedAggregateRange, Probe.EmptyAtEnd, Probe.NullWhole, Probe.NullRangeFirst, Probe.NegativeIndex,
    Probe.NegativeLength, Probe.OverflowRange, Probe.ZeroPastEnd];
File.WriteAllText(args[0], JsonSerializer.Serialize(new
{
    runtime = Environment.Version.ToString(),
    assembly = typeof(Probe).Assembly.Location,
    dependencies = new[] { typeof(NetStandardRange).Assembly.Location },
    cases = cases.Select(run => new
    {
        name = run.Method.Name,
        row = run.Method.MetadataToken & 0xffffff,
        expected = run()
    })
}));
