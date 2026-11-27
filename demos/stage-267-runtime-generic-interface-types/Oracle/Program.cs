using System.Text.Json;
using RuntimeGenericInterfaces;

if (IntPtr.Size != 4) throw new InvalidOperationException("Expected Windows x86 CLR.");
Func<int>[] inputs = [Probe.StringIdentity, Probe.RuntimeChoice, Probe.SeparateDefinitions,
    Probe.NullArgument, Probe.ArrayTypeArgument, Probe.RepeatWithoutClosedToken, Probe.EnumTypeArgument,
    Probe.ObjectDiscoveredArgument, Probe.NominalPrimitiveLayout, Probe.BareNominalType, Probe.VoidTypeArgument];
var runtime = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
File.WriteAllText(args[0], JsonSerializer.Serialize(new
{
    runtime = Environment.Version.ToString(),
    assembly = typeof(Probe).Assembly.Location,
    dependencies = new[] { Path.Combine(runtime, "netstandard.dll"), Path.Combine(runtime, "System.Runtime.dll"),
        Environment.GetEnvironmentVariable("STANDARDWASM_MONO_CORELIB") ?? typeof(object).Assembly.Location },
    cases = inputs.Select(run => new { name = run.Method.Name, row = run.Method.MetadataToken & 0xffffff, expected = run() })
}));
