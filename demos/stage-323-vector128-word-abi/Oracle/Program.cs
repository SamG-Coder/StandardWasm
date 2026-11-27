using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Text.Json;

if (IntPtr.Size != 4) throw new InvalidOperationException("Use the Windows x86 CLR.");
string runtime = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
string mono = Environment.GetEnvironmentVariable("STANDARDWASM_MONO_CORELIB") ??
    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages",
        "microsoft.netcore.app.runtime.mono.browser-wasm", Environment.Version.ToString(), "runtimes",
        "browser-wasm", "native", "System.Private.CoreLib.dll");
var cases = typeof(VectorWordProbe).GetMethods(BindingFlags.Public | BindingFlags.Static).Select(method =>
{
    int row = method.MetadataToken & 0xffffff;
    int expected;
    int? unsupportedPc = null;
    if (method.Name == nameof(VectorWordProbe.ExportedVector) || method.Name == nameof(VectorWordProbe.BoxedVector))
    {
        expected = ((Vector128<ushort>)method.Invoke(null, null)!).GetElement(0);
        unsupportedPc = method.Name == nameof(VectorWordProbe.ExportedVector) ? -1 : 7;
    }
    else if (method.Name == nameof(VectorWordProbe.InvalidLane))
    {
        try { method.Invoke(null, null); }
        catch (TargetInvocationException error) when (error.InnerException is ArgumentOutOfRangeException)
        {
            return new { name = method.Name, row, expected = 0, rejection = (object?)new { status = -2, location = new[] { 0, row, 7 } } };
        }
        throw new InvalidOperationException("The CLR accepted an invalid vector lane.");
    }
    else
    {
        expected = (int)method.Invoke(null, null)!;
        if (method.Name == nameof(VectorWordProbe.ForeignLane)) unsupportedPc = 2;
    }
    int point = row;
    if (method.Name == nameof(VectorWordProbe.DynamicLane))
    {
        point = typeof(VectorWordProbe).GetMethod("ReadLane", BindingFlags.NonPublic | BindingFlags.Static)!.MetadataToken & 0xffffff;
        unsupportedPc = 2;
    }
    return new { name = method.Name, row, expected, rejection = unsupportedPc.HasValue
        ? (object?)new { status = -2, location = new[] { 0, point, unsupportedPc.Value } } : null };
});
File.WriteAllText(args[0], JsonSerializer.Serialize(new
{
    runtime = Environment.Version + "/" + RuntimeInformation.ProcessArchitecture,
    assembly = typeof(VectorWordProbe).Assembly.Location,
    dependencies = new[] { Path.Combine(runtime, "System.Runtime.dll"), Path.Combine(runtime, "System.Runtime.Intrinsics.dll"),
        Path.Combine(runtime, "System.Memory.dll"), Path.Combine(runtime, "System.Runtime.InteropServices.dll"), mono },
    cases,
}));
