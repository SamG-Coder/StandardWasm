#nullable enable
using System.Runtime.InteropServices;
using System.Text.Json;
using ReadOnlyArrayAssignment;

if (IntPtr.Size != 4) throw new InvalidOperationException("Use the Windows x86 CLR.");
Func<int>[] inputs = [ Probe.ValueList, Probe.ReferenceCollection, Probe.NullList, Probe.NullCollection,
    Probe.RequiresVariance, Probe.RequiresDispatch ];
string core = typeof(object).Assembly.Location;
string runtime = Path.GetDirectoryName(core)!;
string coreLib = Environment.GetEnvironmentVariable("STANDARDWASM_MONO_CORELIB") ?? core;
var cases = inputs.Select(run =>
{
    int row = run.Method.MetadataToken & 0xffffff;
    object? rejection = null;
    if (run.Method.Name is nameof(Probe.RequiresDispatch) or nameof(Probe.RequiresVariance))
    {
        bool dispatch = run.Method.Name == nameof(Probe.RequiresDispatch);
        byte[] code = run.Method.GetMethodBody()!.GetILAsByteArray()!;
        // Each control is a straight-line allocation followed by its single call.
        int call = Array.IndexOf(code, dispatch ? (byte)0x6f : (byte)0x28);
        if (call < 0 || code.Length != call + 6 || code[^1] != 0x2a)
            throw new InvalidDataException("Expected a call immediately before ret.");
        rejection = new { status = dispatch ? -2 : -1, location = new[] { 0, row, call } };
    }
    return new { name = run.Method.Name, row, expected = run(), rejection };
});
File.WriteAllText(args[0], JsonSerializer.Serialize(new
{
    runtime = Environment.Version + "/" + RuntimeInformation.ProcessArchitecture,
    assembly = typeof(Probe).Assembly.Location,
    dependencies = new[] { Path.Combine(runtime, "netstandard.dll"), Path.Combine(runtime, "System.Runtime.dll"), coreLib },
    cases
}));
