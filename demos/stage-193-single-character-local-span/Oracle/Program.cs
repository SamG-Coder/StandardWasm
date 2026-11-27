using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using SingleCharacterLocalSpan;

if (IntPtr.Size != 4) throw new InvalidOperationException("Expected the x86 CLR.");
Func<int>[] cases = [Probe.LiveLocal, Probe.LocalRefWrite, Probe.DistinctAnchors, Probe.EndAnchors, Probe.SameFrameRead, Probe.SeparateFrames];
File.WriteAllText(args[0], JsonSerializer.Serialize(new
{
    runtime = Environment.Version.ToString(),
    assembly = typeof(Probe).Assembly.Location,
    cases = cases.Select(run => new { name = run.Method.Name, row = run.Method.MetadataToken & 0xffffff, expected = run() })
}));
