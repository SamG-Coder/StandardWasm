#nullable enable
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using ConstrainedInt32Compare;

if (IntPtr.Size != 4) throw new InvalidOperationException("Use the Windows x86 CLR.");
var compare = typeof(Probe).GetMethod("Compare", BindingFlags.Static | BindingFlags.NonPublic)!;
int compareRow = compare.MetadataToken & 0xffffff;
byte[] il = compare.GetMethodBody()!.GetILAsByteArray()!;
int prefix = Array.IndexOf(il, (byte)0xfe);
if (prefix < 0 || prefix + 6 >= il.Length || il[prefix + 1] != 0x16 || il[prefix + 6] != 0x6f)
    throw new InvalidDataException("Expected constrained. followed by callvirt.");

Func<int>[] inputs = [Probe.Less, Probe.Equal, Probe.Greater, Probe.MinToMax, Probe.MaxToMin,
    Probe.DirectCompare, Probe.UnsignedControl];
string core = typeof(object).Assembly.Location;
string runtime = Path.GetDirectoryName(core)!;
string coreLib = Environment.GetEnvironmentVariable("STANDARDWASM_MONO_CORELIB") ?? core;
var cases = inputs.Select(run =>
{
    object? rejection = run.Method.Name == nameof(Probe.UnsignedControl)
        ? new { status = -2, location = new[] { 0, compareRow, prefix } }
        : null;
    return new { name = run.Method.Name, row = run.Method.MetadataToken & 0xffffff, expected = run(), rejection };
});
File.WriteAllText(args[0], JsonSerializer.Serialize(new
{
    runtime = Environment.Version + "/" + RuntimeInformation.ProcessArchitecture,
    assembly = typeof(Probe).Assembly.Location,
    dependencies = new[] { Path.Combine(runtime, "System.Runtime.dll"), coreLib },
    cases
}));
