#nullable enable
extern alias Other;
using System.Runtime.InteropServices;
using System.Text.Json;
using AssemblyTypeNames;
using OtherCandidate = Other::AssemblyTypeNames.Candidate;

if (IntPtr.Size != 4) throw new InvalidOperationException("Use the Windows x86 CLR.");
Func<int>[] inputs = [Probe.DynamicName, Probe.ScopedIdentity, Probe.DefinitionNames, Probe.MissingNames,
    Probe.CoreIdentity, Probe.NullAndEmpty, Probe.CaseFlagOutsideProfile];
string core = typeof(object).Assembly.Location;
string runtime = Path.GetDirectoryName(core)!;
string coreLib = Environment.GetEnvironmentVariable("STANDARDWASM_MONO_CORELIB") ?? core;
var cases = inputs.Select(run =>
{
    int row = run.Method.MetadataToken & 0xffffff;
    object? rejection = null;
    if (run.Method.Name == nameof(Probe.CaseFlagOutsideProfile))
    {
        byte[] il = run.Method.GetMethodBody()!.GetILAsByteArray()!;
        int call = Array.LastIndexOf(il, (byte)0x6f);
        if (call < 0 || call + 4 >= il.Length || il[call + 4] != 0x0a)
            throw new InvalidDataException("Expected the linked Assembly.GetType virtual call.");
        rejection = new { status = -2, location = new[] { 0, row, call } };
    }
    return new { name = run.Method.Name, row, expected = run(), rejection };
});
File.WriteAllText(args[0], JsonSerializer.Serialize(new
{
    runtime = Environment.Version + "/" + RuntimeInformation.ProcessArchitecture,
    assembly = typeof(Probe).Assembly.Location,
    dependencies = new[] { typeof(OtherCandidate).Assembly.Location, Path.Combine(runtime, "System.Runtime.dll"), coreLib },
    cases
}));
