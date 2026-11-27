using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;

// Stage 156 (issue #274, decision 0083). Run on the 32-bit CLR: the fixture's DllImports bind to
// the real x86 kernel32 and ucrtbase, and its results are the expected values. The runtime
// directory supplies the CoreLib and facades the Wasm runner bundles.
if (Environment.Is64BitProcess || IntPtr.Size != 4)
    throw new InvalidOperationException("Run the oracle on a 32-bit CLR.");
string evidence = args[0], fixture = Path.GetFullPath(args[1]);
string runtimeDirectory = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
var files = new List<string> { fixture };
foreach (string name in new[] { "netstandard", "System.Runtime", "System.Runtime.InteropServices", "System.Private.CoreLib" })
    files.Add(Path.Combine(runtimeDirectory, name + ".dll"));
Type kernels = Assembly.LoadFrom(fixture).GetType("NativeImportFixture.Kernels", throwOnError: true)!;
var cases = new List<object>();
foreach (string name in new[] { "MulDivRounds", "MulDivNegative", "MulDivThird", "MulDivNegativeHalf", "MulDivWide",
    "MulDivOverflow", "MulDivByZero", "AbsMinimum", "LongAbs", "SquareRoot", "MaxIgnoresNaN", "MaxOfNegatives",
    "RoundHalfAway", "SignOfNegativeZero", "LastError", "MissingEntry" })
{
    MethodInfo method = kernels.GetMethod(name)!;
    cases.Add(new { name, row = method.MetadataToken & 0xffffff, expected = method.Invoke(null, null)!.ToString() });
}
// A non-blittable signature (a string argument) stays a located -2 at the P/Invoke method itself.
{
    MethodInfo method = kernels.GetMethod("StringArgument")!;
    MethodInfo import = kernels.Assembly.GetType("NativeImportFixture.Native", throwOnError: true)!
        .GetMethod("lstrlenW", BindingFlags.Static | BindingFlags.Public)!;
    cases.Add(new
    {
        name = "StringArgument", row = method.MetadataToken & 0xffffff, expected = method.Invoke(null, null)!.ToString(),
        status = -2, diagnostic = new[] { 0, import.MetadataToken & 0xffffff, -1 }
    });
}
File.WriteAllText(Path.Combine(evidence, "oracle.json"), JsonSerializer.Serialize(new
{
    files, cases, runtime = Environment.Version.ToString(), process = RuntimeInformation.ProcessArchitecture.ToString()
}));
