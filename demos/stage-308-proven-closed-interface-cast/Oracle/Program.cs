using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;

if (IntPtr.Size != 4) throw new InvalidOperationException("Use the Windows x86 CLR.");
string runtime = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
string mono = Environment.GetEnvironmentVariable("STANDARDWASM_MONO_CORELIB") ??
    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages",
        "microsoft.netcore.app.runtime.mono.browser-wasm", Environment.Version.ToString(), "runtimes",
        "browser-wasm", "native", "System.Private.CoreLib.dll");
Func<int>[] tests = [ClosedComparerCastProbe.ExactClass, ClosedComparerCastProbe.InheritedClass,
    ClosedComparerCastProbe.Null, ClosedComparerCastProbe.WrongArgument, ClosedComparerCastProbe.Widened];
_ = CastRow("CastText");
_ = CastRow("CastDerived");
File.WriteAllText(args[0], JsonSerializer.Serialize(new
{
    runtime = Environment.Version + "/" + RuntimeInformation.ProcessArchitecture,
    assembly = typeof(ClosedComparerCastProbe).Assembly.Location,
    dependencies = new[] { Path.Combine(runtime, "netstandard.dll"), Path.Combine(runtime, "System.Runtime.dll"), mono },
    cases = tests.Select((test, index) => new
    {
        name = test.Method.Name,
        row = test.Method.MetadataToken & 0xffffff,
        expected = Run(test),
        rejection = index < 3 ? null : new
        {
            status = -2,
            location = new[] { 0, CastRow(index == 3 ? "CastWrong" : "CastWidened"), 1 }
        }
    })
}));

static int Run(Func<int> test)
{
    try { return test(); }
    catch (InvalidCastException) { return -2; }
}

static int CastRow(string name)
{
    MethodInfo method = typeof(ClosedComparerCastProbe).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)!;
    byte[] body = method.GetMethodBody()!.GetILAsByteArray()!;
    if (body.Length != 7 || body[1] != 0x74) throw new InvalidOperationException("Expected an actual castclass.");
    return method.MetadataToken & 0xffffff;
}
