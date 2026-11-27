#nullable enable
using System.Runtime.InteropServices;
using System.Text.Json;

if (IntPtr.Size != 4) throw new InvalidOperationException("Use the Windows x86 CLR.");
Func<int>[] inputs = [Probe.ByteSize, Probe.IntSize, Probe.NestedSize, Probe.VersionInfoSize, Probe.UnverifiedSize, Probe.ExplicitPacking];
string core = typeof(object).Assembly.Location;
string runtime = Path.GetDirectoryName(core)!;
string coreLib = Environment.GetEnvironmentVariable("STANDARDWASM_MONO_CORELIB") ?? core;
var cases = inputs.Select((run, index) =>
{
    int row = run.Method.MetadataToken & 0xffffff;
    byte[] il = run.Method.GetMethodBody()!.GetILAsByteArray()!;
    int sizeofOffset = Array.IndexOf(il, (byte)0xfe);
    if (sizeofOffset < 0 || sizeofOffset + 1 >= il.Length || il[sizeofOffset + 1] != 0x1c)
        throw new InvalidDataException("Expected sizeof in every case.");
    return new
    {
        name = run.Method.Name,
        row,
        expected = run(),
        rejection = index >= 4 ? new { status = -2, location = new[] { 0, row, sizeofOffset } } : null
    };
});
File.WriteAllText(args[0], JsonSerializer.Serialize(new
{
    runtime = Environment.Version + "/" + RuntimeInformation.ProcessArchitecture,
    assembly = typeof(Probe).Assembly.Location,
    dependencies = new[] { Path.Combine(runtime, "netstandard.dll"), Path.Combine(runtime, "System.Runtime.dll"), coreLib },
    cases
}));
