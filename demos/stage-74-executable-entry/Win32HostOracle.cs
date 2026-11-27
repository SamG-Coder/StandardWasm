#:property PublishAot=false
#:property TargetFramework=net10.0-windows
#:property UseWindowsForms=true
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
int[] compiledFixture = [Win32ErrorProbe.Exchange(19), Win32ErrorProbe.Exchange(-13), Win32ErrorProbe.Read(),
    Win32ErrorProbe.LookupModule(0), Win32ErrorProbe.LookupModule(1),
    Win32ErrorProbe.LookupModule(2), Win32ErrorProbe.LookupModule(3)];
var forms = typeof(System.Windows.Forms.Application).Module;
nint moduleHandle = Lookup(forms.Name.ToUpperInvariant());
int moduleError = Marshal.GetLastPInvokeError();
Console.WriteLine(JsonSerializer.Serialize(new { executable, results, errors = new[] { system, pinvoke },
    compiledFixture, framework = forms.Assembly.Location, frameworkLookup = new[] { moduleHandle == 0 ? 0 : 1, moduleError } }));

[DllImport("KERNEL32.dll", EntryPoint = "GetModuleHandleW", ExactSpelling = true,
    CharSet = CharSet.Unicode, SetLastError = true)]
static extern nint Lookup(string? name);
