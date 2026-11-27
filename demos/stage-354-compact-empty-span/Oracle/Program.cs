using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;

if (IntPtr.Size != 4) throw new InvalidOperationException("Use the Windows x86 CLR.");
string runtime = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
string core = Environment.GetEnvironmentVariable("STANDARDWASM_MONO_CORELIB") ??
    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages",
        "microsoft.netcore.app.runtime.mono.browser-wasm", Environment.Version.ToString(), "runtimes",
        "browser-wasm", "native", "System.Private.CoreLib.dll");
var cases = typeof(EmptySpanProbe).GetMethods(BindingFlags.Public | BindingFlags.Static).Select(method =>
{
    int row = method.MetadataToken & 0xffffff;
    return new
    {
        name = method.Name,
        row,
        expected = (int)method.Invoke(null, null)!,
        rejection = method.Name == nameof(EmptySpanProbe.OtherLane) ? new { status = -2, location = new[] { 0, row, 0 } } : null
    };
});
File.WriteAllText(args[0], JsonSerializer.Serialize(new
{
    runtime = Environment.Version + "/" + RuntimeInformation.ProcessArchitecture,
    assembly = typeof(EmptySpanProbe).Assembly.Location,
    dependencies = new[] { Path.Combine(runtime, "System.Runtime.dll"), Path.Combine(runtime, "System.Memory.dll"),
        Path.Combine(runtime, "System.Runtime.InteropServices.dll"), Path.Combine(runtime, "System.Runtime.CompilerServices.Unsafe.dll"), core },
    cases
}));
