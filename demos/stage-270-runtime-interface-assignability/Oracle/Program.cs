using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using RuntimeInterfaceAssignableTo;
using InterfaceRelationContract;

if (IntPtr.Size != 4) throw new InvalidOperationException("Expected Windows x86 CLR.");
Func<int>[] cases = [Probe.Transitive, Probe.ExactClosedArguments, Probe.ScalarWithNominalLayout,
    Probe.StructInterface, Probe.EnumWithoutInterface, Probe.NullTarget, Probe.NullReceiver, Probe.NullReceiverAndTarget,
    Probe.UnsupportedVariance, Probe.ProvenInterfaceWithMissingEdge, Probe.IncompleteInterfaceClosure];
string runtime = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
string core = Environment.GetEnvironmentVariable("STANDARDWASM_MONO_CORELIB") ?? typeof(object).Assembly.Location;
File.WriteAllText(args[0], JsonSerializer.Serialize(new
{
    runtime = Environment.Version.ToString(),
    assembly = typeof(Probe).Assembly.Location,
    // Leave the bridge assembly out of the Wasm bundle to exercise an incomplete graph.
    dependencies = new[] { Path.Combine(runtime, "netstandard.dll"), Path.Combine(runtime, "System.Runtime.dll"), core,
        typeof(IExternal<>).Assembly.Location },
    cases = cases.Select(run =>
    {
        int row = run.Method.MetadataToken & 0xffffff;
        object rejection = run == Probe.UnsupportedVariance ? new { status = -2, location = new[] { 0, row, 44 } } : null;
        return new { name = run.Method.Name, row, expected = run(), rejection, trap = run == Probe.IncompleteInterfaceClosure };
    })
}));
