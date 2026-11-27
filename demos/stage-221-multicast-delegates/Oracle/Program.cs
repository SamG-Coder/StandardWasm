using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;

// Stage 221 (issue #256, decision 0092). Run on the 32-bit CLR: its runtime directory supplies the
// CoreLib and facades the Wasm runner bundles, and its results are the expected values.
if (Environment.Is64BitProcess || IntPtr.Size != 4)
    throw new InvalidOperationException("Run the oracle on a 32-bit CLR.");
string evidence = args[0], fixture = Path.GetFullPath(args[1]);
string runtimeDirectory = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
var files = new List<string> { fixture };
foreach (string name in new[] { "netstandard", "System.Runtime", "System.Private.CoreLib" })
    files.Add(Path.Combine(runtimeDirectory, name + ".dll"));
Type kernels = Assembly.LoadFrom(fixture).GetType("MulticastDelegateFixture.Kernels", throwOnError: true)!;
var cases = new List<object>();
foreach (MethodInfo method in kernels.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.DeclaredOnly)
    .OrderBy(method => method.MetadataToken))
{
    // Reject* kernels run on the CLR too; the compiler must diagnose them (status -2) instead.
    bool rejected = method.Name.StartsWith("Reject", StringComparison.Ordinal);
    cases.Add(new { name = method.Name, row = method.MetadataToken & 0xffffff, rejected,
        expected = method.Invoke(null, null)!.ToString() });
}
File.WriteAllText(Path.Combine(evidence, "oracle.json"), JsonSerializer.Serialize(new
{
    files, cases, runtime = Environment.Version.ToString(), process = RuntimeInformation.ProcessArchitecture.ToString()
}));
