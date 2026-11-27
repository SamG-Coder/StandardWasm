using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using ReadOnlyCharacterReferences;

if (IntPtr.Size != 4) throw new InvalidOperationException("Expected the x86 CLR.");
Func<int>[] cases = [Probe.StringInterior, Probe.ArrayInterior, Probe.ManagedEmptyAnchors,
    Probe.NativeAnchors, Probe.WritableAlias, Probe.UnverifiedRefCall,
    Probe.UnverifiedWriter, Probe.UnverifiedEscape];
File.WriteAllText(args[0], JsonSerializer.Serialize(new
{
    runtime = Environment.Version.ToString(),
    assembly = typeof(Probe).Assembly.Location,
    cases = cases.Select(run =>
    {
        string name = run.Method.Name;
        var rejected = name == nameof(Probe.UnverifiedWriter) ? nameof(Probe.WriteThrough) :
            name == nameof(Probe.UnverifiedEscape) ? nameof(Probe.Escape) :
            name == nameof(Probe.UnverifiedRefCall) ? nameof(Probe.CallThrough) : null;
        return new
        {
            name,
            row = run.Method.MetadataToken & 0xffffff,
            expected = run(),
            memory = name == nameof(Probe.NativeAnchors),
            rejection = rejected is null ? null : new
            {
                status = -2,
                location = new[] { 0, typeof(Probe).GetMethod(rejected)!.MetadataToken & 0xffffff,
                    name == nameof(Probe.UnverifiedWriter) ? 8 : 6 }
            }
        };
    })
}));
