using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;

if (IntPtr.Size != 4) throw new InvalidOperationException("Use the Windows x86 CLR.");
string runtime = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
string mono = Environment.GetEnvironmentVariable("STANDARDWASM_MONO_CORELIB") ??
    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages",
        "microsoft.netcore.app.runtime.mono.browser-wasm", Environment.Version.ToString(), "runtimes",
        "browser-wasm", "native", "System.Private.CoreLib.dll");
Func<int>[] cases = [ReadOnlyCharacterProbe.Left, ReadOnlyCharacterProbe.Right, ReadOnlyCharacterProbe.Backward,
    ReadOnlyCharacterProbe.Empty, ReadOnlyCharacterProbe.Writable, ReadOnlyCharacterProbe.RejectedWrite,
    ReadOnlyCharacterProbe.RejectedEscape, ReadOnlyCharacterProbe.OutOfOwner];
File.WriteAllText(args[0], JsonSerializer.Serialize(new
{
    runtime = Environment.Version + "/" + RuntimeInformation.ProcessArchitecture,
    assembly = typeof(ReadOnlyCharacterProbe).Assembly.Location,
    dependencies = new[] { Path.Combine(runtime, "System.Runtime.dll"), Path.Combine(runtime, "System.Memory.dll"),
        Path.Combine(runtime, "System.Runtime.InteropServices.dll"), Path.Combine(runtime, "System.Runtime.CompilerServices.Unsafe.dll"), mono },
    cases = cases.Select((run, index) => new
    {
        name = run.Method.Name,
        row = run.Method.MetadataToken & 0xffffff,
        expected = index == 7 ? 0 : run(),
        trap = index == 7,
        rejection = index < 5 || index == 7 ? null : new
        {
            status = -2,
            location = index == 5 ? new[] { 0, Row("Write"), 3 } : new[] { 0, Row("Escape"), 1 }
        }
    })
}));

static int Row(string name) => typeof(ReadOnlyCharacterProbe).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)!
    .MetadataToken & 0xffffff;
