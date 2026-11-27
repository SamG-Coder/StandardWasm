using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using ScopedNativeFrameLayout;

if (IntPtr.Size != 4) throw new InvalidOperationException("Expected Windows x86 CLR.");
Func<int>[] cases = [Probe.WriteAndClear, Probe.IndependentOwners, Probe.ReborrowAfterReplacement,
    Probe.ManagedFieldDiagnostic, Probe.ByrefFieldDiagnostic];
File.WriteAllText(args[0], JsonSerializer.Serialize(new
{
    runtime = Environment.Version.ToString(),
    assembly = typeof(Probe).Assembly.Location,
    dependencies = new[] { typeof(object).Assembly.Location },
    cases = cases.Select(run =>
    {
        int row = run.Method.MetadataToken & 0xffffff;
        bool managed = run.Method.Name == nameof(Probe.ManagedFieldDiagnostic);
        bool byref = run.Method.Name == nameof(Probe.ByrefFieldDiagnostic);
        return new
        {
            name = run.Method.Name, row, expected = run(), memory = !managed && !byref,
            rejection = managed ? new { status = -2, location = new[] { 0, typeof(ManagedScope)
                .GetMethod("Borrow", BindingFlags.Public | BindingFlags.Static)!.MetadataToken & 0xffffff, 1 } } :
                byref ? new { status = -2, location = new[] { 0, row, -1 } } : null
        };
    })
}));
