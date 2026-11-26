#:property PublishAot=false
#:project ./Win32Fixture/Win32Fixture.csproj
using System.Runtime.InteropServices;
using System.Text.Json;

string executable = Path.GetFileName(Environment.ProcessPath)!;
nint original = Lookup(null);
var results = new List<int[]>();
foreach (string? name in new[] { (string?)null, executable.ToUpperInvariant(),
    "missing-standardwasm-native-module.dll", "", executable + "\0suffix" })
{
    nint handle = Lookup(name);
    int error = Marshal.GetLastPInvokeError();
    results.Add([handle == 0 ? 0 : handle == original ? 1 : -1, error]);
}

Marshal.SetLastPInvokeError(73);
Marshal.SetLastSystemError(19);
int system = Marshal.GetLastSystemError();
int pinvoke = Marshal.GetLastPInvokeError();
Marshal.SetLastPInvokeError(0);
int[] compiledFixture = [Win32ErrorProbe.Exchange(19), Win32ErrorProbe.Exchange(-13), Win32ErrorProbe.Read()];
Console.WriteLine(JsonSerializer.Serialize(new { executable, results, errors = new[] { system, pinvoke }, compiledFixture }));

[DllImport("KERNEL32.dll", EntryPoint = "GetModuleHandleW", ExactSpelling = true,
    CharSet = CharSet.Unicode, SetLastError = true)]
static extern nint Lookup(string? name);
