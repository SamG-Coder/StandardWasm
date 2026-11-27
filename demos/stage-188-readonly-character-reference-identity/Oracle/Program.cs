using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using ReadOnlyCharacterReferenceIdentity;

if (IntPtr.Size != 4) throw new InvalidOperationException("Expected the x86 CLR.");
Func<int>[] cases = [Probe.StringInterior, Probe.GenericArrayInterior, Probe.NativeInterior,
    Probe.EmptyAnchors, Probe.WritableAlias, Probe.UnverifiedWriter, Probe.UnverifiedRefCall];
File.WriteAllText(args[0], JsonSerializer.Serialize(new
{
    runtime = Environment.Version.ToString(),
    assembly = typeof(Probe).Assembly.Location,
    cases = cases.Select(run =>
    {
        string name = run.Method.Name;
        var rejected = name == nameof(Probe.UnverifiedWriter) ? nameof(Probe.WriteThrough) :
            name == nameof(Probe.UnverifiedRefCall) ? nameof(Probe.CallThrough) : null;
        return new
        {
            name,
            row = run.Method.MetadataToken & 0xffffff,
            expected = run(),
            memory = name == nameof(Probe.NativeInterior),
            rejection = rejected is null ? null : new
            {
                status = -2,
                location = new[] { 0, typeof(Probe).GetMethod(rejected)!.MetadataToken & 0xffffff,
                    name == nameof(Probe.UnverifiedWriter) ? 13 : 11 }
            }
        };
    })
}));
