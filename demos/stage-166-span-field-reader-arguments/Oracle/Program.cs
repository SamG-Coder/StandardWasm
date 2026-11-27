using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using SpanFieldReaderArguments;

if (IntPtr.Size != 4) throw new InvalidOperationException("Expected the x86 CLR.");
Func<int>[] cases = [Probe.IndexThroughFieldReader, Probe.SliceThroughFieldReader,
    Probe.OutOfRangeThroughFieldReader, Probe.MutatingArgumentMustStayDiagnosed];
int mutatingCaller = typeof(ReplacingBuffer).GetMethod(nameof(ReplacingBuffer.Write))!.MetadataToken & 0xffffff;
File.WriteAllText(args[0], JsonSerializer.Serialize(new
{
    runtime = Environment.Version.ToString(),
    assembly = typeof(Probe).Assembly.Location,
    cases = cases.Select(run => new
    {
        name = run.Method.Name,
        row = run.Method.MetadataToken & 0xffffff,
        expected = run(),
        rejection = run.Method.Name == nameof(Probe.MutatingArgumentMustStayDiagnosed)
            ? new { status = -2, location = new[] { 0, mutatingCaller, 1 } } : null
    })
}));
