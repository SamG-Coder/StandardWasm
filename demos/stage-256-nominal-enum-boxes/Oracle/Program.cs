using System.Text.Json;
using EnumBoxes;

if (IntPtr.Size != 4) throw new InvalidOperationException("Expected Windows x86 CLR.");
Func<int>[] inputs = [Probe.NominalType, Probe.AllocationIdentity, Probe.CompatibleUnbox, Probe.IncompatibleUnbox,
    Probe.NarrowAndWide, Probe.CoreInterface, Probe.NullUnbox, Probe.VirtualDiagnostic];
var runtime = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
var cases = inputs.Select(run =>
{
    int row = run.Method.MetadataToken & 0xffffff;
    object? rejection = null;
    if (run.Method.Name == nameof(Probe.VirtualDiagnostic))
    {
        byte[] code = run.Method.GetMethodBody()!.GetILAsByteArray()!;
        int call = Array.IndexOf(code, (byte)0x6f);
        if (call < 0 || code[call + 4] != 0x0a)
            throw new InvalidDataException("Expected the virtual Object call.");
        rejection = new { status = -2, location = new[] { 0, row, call } };
    }
    return new { name = run.Method.Name, row, expected = run(), rejection };
});
File.WriteAllText(args[0], JsonSerializer.Serialize(new
{
    runtime = Environment.Version.ToString(),
    assembly = typeof(Probe).Assembly.Location,
    dependencies = new[] { Path.Combine(runtime, "netstandard.dll"), Path.Combine(runtime, "System.Runtime.dll"),
        Environment.GetEnvironmentVariable("STANDARDWASM_MONO_CORELIB") ?? typeof(object).Assembly.Location },
    cases
}));
