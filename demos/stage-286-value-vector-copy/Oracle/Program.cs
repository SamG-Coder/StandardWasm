using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using ValueVectorCopy;

if (IntPtr.Size != 4) throw new InvalidOperationException("Expected Windows x86 CLR.");
Func<int>[] cases = [Probe.ExactCopyAndReferencePayload, Probe.RightOverlap, Probe.LeftOverlap,
    Probe.DestinationAlias, Probe.IndependentSlots, Probe.DefaultSnapshots, Probe.EmptyCopy,
    Probe.TypedNullBeforeLength, Probe.TypedNegativeLength, Probe.TypedSourceRangeBeforeDestinationIndex,
    Probe.NoWriteOnDestinationRangeFailure, Probe.PrimitiveRegression];
string runtime = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
string mono = Environment.GetEnvironmentVariable("STANDARDWASM_MONO_RUNTIME");
string facades = mono == null ? runtime : Path.Combine(mono, "lib", "net10.0");
string core = mono == null ? typeof(object).Assembly.Location : Path.Combine(mono, "native", "System.Private.CoreLib.dll");
File.WriteAllText(args[0], JsonSerializer.Serialize(new
{
    runtime = Environment.Version.ToString(),
    assembly = typeof(Probe).Assembly.Location,
    dependencies = new[] { Path.Combine(facades, "netstandard.dll"), Path.Combine(facades, "System.Runtime.dll"), core },
    cases = cases.Select(run => new
    {
        name = run.Method.Name,
        row = run.Method.MetadataToken & 0xffffff,
        // Retail Mono validates both indices before it checks the source range.
        expected = mono != null && run.Method.Name == nameof(Probe.TypedSourceRangeBeforeDestinationIndex) ? 147 : run()
    })
}));
