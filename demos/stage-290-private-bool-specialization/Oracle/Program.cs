#nullable enable
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using FalseBooleanSpecialization;

if (IntPtr.Size != 4) throw new InvalidOperationException("Use the Windows x86 CLR.");
Func<int>[] inputs = [Probe.IndependentCallers, Probe.ReceiverEvaluation, Probe.Recursion,
    Probe.OrdinaryAndClone, Probe.MethodAddress, Probe.AlternateEntry, Probe.ExceptionEvaluation, Probe.TruePathRequiresUnsupported,
    Probe.DynamicPathRequiresUnsupported];
string core = typeof(object).Assembly.Location;
string runtime = Path.GetDirectoryName(core)!;
var unsupported = typeof(Probe).GetMethod("Unsupported", BindingFlags.Static | BindingFlags.NonPublic)!;
int unsupportedRow = unsupported.MetadataToken & 0xffffff;
var cases = inputs.Select(run =>
{
    object? rejection = run.Method.Name is nameof(Probe.TruePathRequiresUnsupported) or nameof(Probe.DynamicPathRequiresUnsupported)
        ? new { status = -2, location = new[] { 0, unsupportedRow, 2 } }
        : null;
    return new { name = run.Method.Name, row = run.Method.MetadataToken & 0xffffff, expected = run(), rejection };
});
File.WriteAllText(args[0], JsonSerializer.Serialize(new
{
    runtime = Environment.Version + "/" + RuntimeInformation.ProcessArchitecture,
    assembly = typeof(Probe).Assembly.Location,
    dependencies = new[] { Path.Combine(runtime, "System.Runtime.dll"), core },
    cases
}));
