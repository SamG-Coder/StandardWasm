using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;

if (IntPtr.Size != 4) throw new InvalidOperationException("Use the Windows x86 CLR.");
string runtime = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
string core = Environment.GetEnvironmentVariable("STANDARDWASM_MONO_CORELIB") ??
    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages",
        "microsoft.netcore.app.runtime.mono.browser-wasm", Environment.Version.ToString(), "runtimes",
        "browser-wasm", "native", "System.Private.CoreLib.dll");
Func<int>[] cases = [GenericCharacterProbe.ReadScalar, GenericCharacterProbe.ReadSimd, GenericCharacterProbe.SiblingSpecializations,
    GenericCharacterProbe.ReversedOrder, GenericCharacterProbe.RejectedWrite, GenericCharacterProbe.RejectedEscape];
File.WriteAllText(args[0], JsonSerializer.Serialize(new
{
    runtime = Environment.Version + "/" + RuntimeInformation.ProcessArchitecture,
    assembly = typeof(GenericCharacterProbe).Assembly.Location,
    dependencies = new[] { Path.Combine(runtime, "System.Runtime.dll"), Path.Combine(runtime, "System.Memory.dll"),
        Path.Combine(runtime, "System.Runtime.InteropServices.dll"), Path.Combine(runtime, "System.Runtime.CompilerServices.Unsafe.dll"),
        Path.Combine(runtime, "System.Runtime.Intrinsics.dll"), core },
    cases = cases.Select((run, index) => new
    {
        name = run.Method.Name,
        row = run.Method.MetadataToken & 0xffffff,
        expected = run(),
        rejection = index < 4 ? null : new
        {
            status = -2,
            location = index == 4 ? new[] { 0, Row("Write"), 3 } : new[] { 0, Row("Escape"), 1 }
        }
    })
}));

static int Row(string name) => typeof(GenericCharacterProbe).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)!
    .MetadataToken & 0xffffff;
