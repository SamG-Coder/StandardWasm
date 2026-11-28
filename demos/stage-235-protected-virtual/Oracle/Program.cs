#nullable enable
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using ProtectedVirtual;

if (IntPtr.Size != 4) throw new InvalidOperationException("Expected Windows x86 CLR.");
Func<int>[] cases = [Probe.BaseBody, Probe.OverrideBody, Probe.DynamicReceiver, Probe.NullReceiver, Probe.DerivedAccessDiagnostic];
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
        int derived = typeof(DerivedThroughBase).GetMethod(nameof(DerivedThroughBase.AddToDerived))!.MetadataToken & 0xffffff;
        return new
        {
            name = run.Method.Name,
            row,
            expected = run(),
            rejection = run.Method.Name == nameof(Probe.DerivedAccessDiagnostic)
                ? new { status = -6, location = new[] { 0, derived, 3 } } : null
        };
    })
}));
