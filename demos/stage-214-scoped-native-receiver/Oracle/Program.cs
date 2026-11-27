using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using ScopedNativeReceiver;

if (IntPtr.Size != 4) throw new InvalidOperationException("Expected Windows x86 CLR.");
Func<int>[] cases = [Probe.ValueAndFinally, Probe.ExceptionalFinally, Probe.IndependentAliases,
    Probe.EscapedAddressDiagnostic];
File.WriteAllText(args[0], JsonSerializer.Serialize(new
{
    runtime = Environment.Version.ToString(),
    assembly = typeof(Probe).Assembly.Location,
    dependencies = new[] { typeof(object).Assembly.Location },
    cases = cases.Select(run =>
    {
        int row = run.Method.MetadataToken & 0xffffff;
        bool escapes = run.Method.Name == nameof(Probe.EscapedAddressDiagnostic);
        return new
        {
            name = run.Method.Name, row, expected = run(), memory = !escapes,
            rejection = escapes ? new { status = -2, location = new[] { 0, row, 8 } } : null
        };
    })
}));
