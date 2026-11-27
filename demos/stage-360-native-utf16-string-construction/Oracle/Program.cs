using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;

if (IntPtr.Size != 4) throw new InvalidOperationException("Use the Windows x86 CLR.");
NativeStringProbe.CheckClrGuards();
string runtime = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
string core = Environment.GetEnvironmentVariable("STANDARDWASM_MONO_CORELIB") ??
    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages",
        "microsoft.netcore.app.runtime.mono.browser-wasm", Environment.Version.ToString(), "runtimes",
        "browser-wasm", "native", "System.Private.CoreLib.dll");
Func<int>[] cases = [NativeStringProbe.Snapshot, NativeStringProbe.ZeroLength, NativeStringProbe.DirectConstructor,
    NativeStringProbe.OutsideWasmMemory];
File.WriteAllText(args[0], JsonSerializer.Serialize(new
{
    runtime = Environment.Version + "/" + RuntimeInformation.ProcessArchitecture,
    assembly = typeof(NativeStringProbe).Assembly.Location,
    dependencies = new[] { Path.Combine(runtime, "System.Runtime.dll"), core },
    cases = cases.Select((run, index) => new
    {
        name = run.Method.Name,
        row = run.Method.MetadataToken & 0xffffff,
        expected = index == 3 ? 0 : run(),
        memory = true,
        trap = index == 3,
        rejection = index == 2 ? new { status = -2, location = core.StartsWith(runtime, StringComparison.OrdinalIgnoreCase)
            ? new[] { 2, 15148, 444 } : new[] { 2, 30055, 47 } } : null
    })
}));
