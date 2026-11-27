using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;

if (IntPtr.Size != 4) throw new InvalidOperationException("Use the Windows x86 CLR.");
var probe = typeof(Probe);
var shared = probe.GetMethod("SharedConsumer", BindingFlags.Static | BindingFlags.NonPublic)!;
int sharedRow = shared.MetadataToken & 0xffffff;
string core = typeof(object).Assembly.Location;
string runtime = Path.GetDirectoryName(core)!;
string coreLib = Environment.GetEnvironmentVariable("STANDARDWASM_MONO_CORELIB") ?? core;
var cases = new[] { nameof(Probe.VirtualIdentity), nameof(Probe.VirtualNull), nameof(Probe.DirectIdentity),
    nameof(Probe.ObjectJoin), nameof(Probe.JoinedInterfaceArgument) }.Select(name =>
{
    var method = probe.GetMethod(name)!;
    var rejection = name == nameof(Probe.JoinedInterfaceArgument)
        ? new { status = -1, location = new[] { 0, sharedRow, 13 } }
        : null;
    return new { name, row = method.MetadataToken & 0xffffff, expected = (int)method.Invoke(null, null)!, rejection };
});
File.WriteAllText(args[0], JsonSerializer.Serialize(new
{
    runtime = Environment.Version + "/" + RuntimeInformation.ProcessArchitecture,
    assembly = probe.Assembly.Location,
    dependencies = new[] { coreLib, Path.Combine(runtime, "System.Runtime.dll") },
    cases
}));
