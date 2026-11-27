using System.Text.Json;
using RuntimeGenericArgumentsDemo;

if (IntPtr.Size != 4) throw new InvalidOperationException("Expected Windows x86 CLR.");
Func<int>[] inputs = [Probe.ClosedValueType, Probe.NestedArgumentOrder, Probe.FreshMutableArray,
    Probe.SharedEmptyArray, Probe.ArrayOfClosedGeneric, Probe.RuntimeArrayIdentity,
    Probe.NullReceiver, Probe.NominalOverride, Probe.Bounds];
var runtime = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
string mono = Environment.GetEnvironmentVariable("STANDARDWASM_MONO_RUNTIME");
string facadeDirectory = mono == null ? runtime : Path.Combine(mono, "lib", "net10.0");
string core = mono == null ? typeof(object).Assembly.Location : Path.Combine(mono, "native", "System.Private.CoreLib.dll");
File.WriteAllText(args[0], JsonSerializer.Serialize(new
{
    runtime = Environment.Version.ToString(),
    assembly = typeof(Probe).Assembly.Location,
    dependencies = new[] { Path.Combine(facadeDirectory, "netstandard.dll"), Path.Combine(facadeDirectory, "System.Runtime.dll"), core },
    cases = inputs.Select(run => new
    {
        name = run.Method.Name, row = run.Method.MetadataToken & 0xffffff,
        // Stock Browser Mono 10.0.10 inspects the array class itself.
        expected = mono != null && run.Method.Name == nameof(Probe.ArrayOfClosedGeneric) ? 0 : run()
    })
}));
