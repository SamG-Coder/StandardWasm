using System.Runtime.InteropServices;
using System.Text.Json;

if (IntPtr.Size != 4) throw new InvalidOperationException("Use the Windows x86 CLR.");
string runtime = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
string mono = Environment.GetEnvironmentVariable("STANDARDWASM_MONO_CORELIB") ??
    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages",
        "microsoft.netcore.app.runtime.mono.browser-wasm", Environment.Version.ToString(), "runtimes",
        "browser-wasm", "native", "System.Private.CoreLib.dll");
Func<int>[] cases = [SignedSpanCastProbe.Positive, SignedSpanCastProbe.Negative, SignedSpanCastProbe.NegativeOne,
    SignedSpanCastProbe.Alias, SignedSpanCastProbe.Empty, SignedSpanCastProbe.Bounds, SignedSpanCastProbe.Writable];
File.WriteAllText(args[0], JsonSerializer.Serialize(new
{
    runtime = Environment.Version + "/" + RuntimeInformation.ProcessArchitecture,
    assembly = typeof(SignedSpanCastProbe).Assembly.Location,
    dependencies = new[] { Path.Combine(runtime, "System.Runtime.dll"), Path.Combine(runtime, "System.Memory.dll"),
        Path.Combine(runtime, "System.Runtime.InteropServices.dll"), mono },
    cases = cases.Select((run, index) => new
    {
        name = run.Method.Name,
        row = run.Method.MetadataToken & 0xffffff,
        expected = run(),
        rejection = index != 6 ? null : new
        {
            status = -2,
            location = new[] { 0, run.Method.MetadataToken & 0xffffff, -1 }
        }
    })
}));
