using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;

if (IntPtr.Size != 4) throw new InvalidOperationException("Use the Windows x86 CLR.");
string runtime = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
string mono = Environment.GetEnvironmentVariable("STANDARDWASM_MONO_CORELIB") ??
    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages",
        "microsoft.netcore.app.runtime.mono.browser-wasm", Environment.Version.ToString(), "runtimes",
        "browser-wasm", "native", "System.Private.CoreLib.dll");
string fixture = typeof(VectorCountProbe).Assembly.Location;
var cases = typeof(VectorCountProbe).GetMethods(BindingFlags.Public | BindingFlags.Static).Select(method =>
{
    int row = method.MetadataToken & 0xffffff;
    if (!method.Name.StartsWith("Invalid", StringComparison.Ordinal))
        return new { name = method.Name, row, expected = (int)method.Invoke(null, null)!, rejection = (object?)null };
    try { method.Invoke(null, null); }
    catch (TargetInvocationException exception) when (exception.InnerException is NotSupportedException)
    {
        return new { name = method.Name, row, expected = 0, rejection = (object?)new { status = -2, location = new[] { 0, row, 0 } } };
    }
    throw new InvalidOperationException("The CLR accepted an unsupported lane.");
});
File.WriteAllText(args[0], JsonSerializer.Serialize(new
{
    runtime = Environment.Version + "/" + RuntimeInformation.ProcessArchitecture,
    assembly = fixture,
    dependencies = new[] { Path.Combine(runtime, "System.Runtime.dll"), Path.Combine(runtime, "System.Runtime.Intrinsics.dll"),
        Path.Combine(Path.GetDirectoryName(fixture)!, "ForeignIntrinsics.dll"), mono },
    cases,
}));
