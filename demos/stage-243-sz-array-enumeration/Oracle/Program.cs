#nullable enable
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using ArrayEnumeration;

if (IntPtr.Size != 4) throw new InvalidOperationException("Expected Windows x86 CLR.");
Func<int>[] cases = [Probe.NonEmpty, Probe.Empty, Probe.IndependentEnumerators, Probe.InvalidCurrent,
    Probe.DirectArray, Probe.MixedDispatch, Probe.NullReceiver, Probe.CovariantTargetDiagnostic];
string runtime = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
string core = Environment.GetEnvironmentVariable("STANDARDWASM_MONO_CORELIB") ?? typeof(object).Assembly.Location;
File.WriteAllText(args[0], JsonSerializer.Serialize(new
{
    runtime = Environment.Version.ToString(),
    assembly = typeof(Probe).Assembly.Location,
    dependencies = new[] { Path.Combine(runtime, "netstandard.dll"), Path.Combine(runtime, "System.Runtime.dll"), core },
    cases = cases.Select(run =>
    {
        int row = run.Method.MetadataToken & 0xffffff;
        return new
        {
            name = run.Method.Name,
            row,
            expected = run(),
            rejection = run.Method.Name == nameof(Probe.CovariantTargetDiagnostic)
                ? new { status = -1, location = new[] { 0, row, 16 } } : null
        };
    })
}));
