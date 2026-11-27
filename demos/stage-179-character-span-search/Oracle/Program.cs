using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using CharacterSpanSearch;

if (IntPtr.Size != 4) throw new InvalidOperationException("Expected the x86 CLR.");
Func<int>[] cases = [Probe.StringSlice, Probe.ArrayAlias, Probe.EmptyAndNoMatch,
    Probe.NativeView, Probe.NeedlesEvaluatedBeforeEmptySearch];
File.WriteAllText(args[0], JsonSerializer.Serialize(new
{
    runtime = Environment.Version.ToString(),
    assembly = typeof(Probe).Assembly.Location,
    cases = cases.Select(run => new
    {
        name = run.Method.Name,
        row = run.Method.MetadataToken & 0xffffff,
        expected = run(),
        memory = run.Method.Name == nameof(Probe.NativeView)
    })
}));
