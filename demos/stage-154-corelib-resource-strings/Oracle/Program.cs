using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;

// Stage 154 (issue #304). Run on the 32-bit CLR: its runtime directory supplies the CoreLib
// the Wasm runner bundles, and its results are the expected values.
if (Environment.Is64BitProcess || IntPtr.Size != 4)
    throw new InvalidOperationException("Run the oracle on a 32-bit CLR.");
string evidence = args[0], fixture = Path.GetFullPath(args[1]);
string runtimeDirectory = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
var files = new[] { fixture, Path.Combine(runtimeDirectory, "System.Runtime.dll"),
    Path.Combine(runtimeDirectory, "System.Private.CoreLib.dll") };
Type kernels = Assembly.LoadFrom(fixture).GetType("ResourceStringFixture.Kernels", throwOnError: true)!;
var cases = new List<object>();
foreach (string name in new[] { "InvalidOperation", "ArgumentNull", "NotSupported", "Format", "KeyNotFound",
    "Overflow", "NotImplemented", "Sequence" })
{
    MethodInfo method = kernels.GetMethod(name)!;
    cases.Add(new { name, row = method.MetadataToken & 0xffffff, expected = method.Invoke(null, null)!.ToString() });
}
File.WriteAllText(Path.Combine(evidence, "oracle.json"), JsonSerializer.Serialize(new
{
    files, cases, runtime = Environment.Version.ToString(), process = RuntimeInformation.ProcessArchitecture.ToString()
}));
