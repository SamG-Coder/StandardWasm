using System.Text.Json;
using TypeNamespaceFixture;

if (IntPtr.Size != 4) throw new InvalidOperationException("Expected an x86 CLR.");
Func<int>[] cases = [Probe.Definitions, Probe.NestedDefinitions, Probe.GenericDefinitions, Probe.GenericConstructed,
    Probe.NestedGenericConstructed, Probe.PlatformDescriptors, Probe.PlatformVectors, Probe.NominalVectors,
    Probe.ClosedVector, Probe.LinkedNested, Probe.RuntimeSelection, Probe.NameAndNamespace, Probe.NullReceiver];
string core = typeof(object).Assembly.Location;
File.WriteAllText(args[0], JsonSerializer.Serialize(new
{
    runtime = Environment.Version.ToString(),
    assembly = typeof(Probe).Assembly.Location,
    dependencies = new[] { typeof(Companion.É.Anchor).Assembly.Location, core,
        Path.Combine(Path.GetDirectoryName(core)!, "netstandard.dll") },
    cases = cases.Select(run => new { name = run.Method.Name, row = run.Method.MetadataToken & 0xffffff, expected = run() })
}));
