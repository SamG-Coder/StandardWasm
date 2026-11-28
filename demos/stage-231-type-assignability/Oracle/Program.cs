#nullable enable
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using TypeAssignability;

if (IntPtr.Size != 4) throw new InvalidOperationException("Expected Windows x86 CLR.");
Func<int>[] cases = [Probe.DynamicInterface, Probe.DynamicBase, Probe.LiteralDescendant, Probe.ClosedSourceTypes, Probe.IdentityAndNull,
    Probe.ArrayDiagnostic, Probe.NullableDiagnostic, Probe.OpenTypeDiagnostic, Probe.VariantTargetDiagnostic,
    Probe.UnprovedReceiverDiagnostic];
string runtime = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
File.WriteAllText(args[0], JsonSerializer.Serialize(new
{
    runtime = Environment.Version.ToString(),
    assembly = typeof(Probe).Assembly.Location,
    dependencies = new[] { Path.Combine(runtime, "netstandard.dll"), Path.Combine(runtime, "System.Runtime.dll"),
        typeof(object).Assembly.Location },
    cases = cases.Select(run =>
    {
        bool unproved = run.Method.Name == nameof(Probe.UnprovedReceiverDiagnostic);
        int row = (unproved ? typeof(Probe).GetMethod("ViaReceiver",
            BindingFlags.NonPublic | BindingFlags.Static)! : run.Method).MetadataToken & 0xffffff;
        return new
        {
            name = run.Method.Name,
            row = run.Method.MetadataToken & 0xffffff,
            expected = run(),
            rejection = run.Method.Name.EndsWith("Diagnostic", StringComparison.Ordinal)
                ? new { status = -2, location = new[] { 0, row, unproved ? 2 : 20 } } : null
        };
    })
}));
