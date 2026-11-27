using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using ReadOnlyAggregateCall;

if (IntPtr.Size != 4) throw new InvalidOperationException("Expected Windows x86 CLR.");
Func<int>[] cases = [Probe.ReadClassAggregate, Probe.PreserveThrownIdentity, Probe.NullFieldReceiver,
    Probe.NullCallReceiver, Probe.ForwardedAddressDiagnostic, Probe.MutatingAddressDiagnostic];
string runtime = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
File.WriteAllText(args[0], JsonSerializer.Serialize(new
{
    runtime = Environment.Version.ToString(),
    assembly = typeof(Probe).Assembly.Location,
    dependencies = new[] { Path.Combine(runtime, "netstandard.dll"), Path.Combine(runtime, "System.Runtime.dll"),
        typeof(object).Assembly.Location },
    cases = cases.Select(run =>
    {
        int row = run.Method.MetadataToken & 0xffffff;
        bool rejected = run.Method.Name == nameof(Probe.ForwardedAddressDiagnostic) ||
            run.Method.Name == nameof(Probe.MutatingAddressDiagnostic);
        int offset = run.Method.Name == nameof(Probe.ForwardedAddressDiagnostic) ? 17 : 13;
        return new
        {
            name = run.Method.Name, row, expected = run(),
            rejection = rejected ? new { status = -2, location = new[] { 0, row, offset } } : null
        };
    })
}));
