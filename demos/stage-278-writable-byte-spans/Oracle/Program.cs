using System.Runtime.InteropServices;
using System.Text.Json;

if (IntPtr.Size != 4) throw new InvalidOperationException("Use the Windows x86 CLR.");
var probe = typeof(Probe);
string defaultCore = typeof(object).Assembly.Location;
string runtime = Path.GetDirectoryName(defaultCore)!;
string coreLib = Environment.GetEnvironmentVariable("STANDARDWASM_MONO_CORELIB") ?? defaultCore;
var cases = new[] { nameof(Probe.Aliases), nameof(Probe.ReadOnlyAlias), nameof(Probe.EmptyRanges),
    nameof(Probe.InvalidRanges), nameof(Probe.SpanBounds) }.Select(name =>
{
    var method = probe.GetMethod(name)!;
    return new { name, row = method.MetadataToken & 0xffffff, expected = (int)method.Invoke(null, null)! };
});
File.WriteAllText(args[0], JsonSerializer.Serialize(new
{
    runtime = Environment.Version + "/" + RuntimeInformation.ProcessArchitecture,
    assembly = probe.Assembly.Location,
    dependencies = new[] { coreLib, Path.Combine(runtime, "System.Runtime.dll"), Path.Combine(runtime, "System.Memory.dll") },
    cases
}));
