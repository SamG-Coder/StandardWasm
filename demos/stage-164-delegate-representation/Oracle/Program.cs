using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;

// Stage 164 (issue #256, decision 0088). Run on the 32-bit CLR: its runtime directory supplies the
// CoreLib and facades the Wasm runner bundles, and its results are the expected values.
if (Environment.Is64BitProcess || IntPtr.Size != 4)
    throw new InvalidOperationException("Run the oracle on a 32-bit CLR.");
string evidence = args[0], fixture = Path.GetFullPath(args[1]);
string runtimeDirectory = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
var files = new List<string> { fixture };
foreach (string name in new[] { "netstandard", "System.Runtime", "System.Private.CoreLib" })
    files.Add(Path.Combine(runtimeDirectory, name + ".dll"));
Type kernels = Assembly.LoadFrom(fixture).GetType("DelegateFixture.Kernels", throwOnError: true)!;
var cases = new List<object>();
foreach (string name in new[] { "CachedLambda", "CachedLambdaTwice", "CachedLambdaInTry", "StaticMethodGroup",
    "StaticInStaticField", "StaticInInstanceField", "StaticEventHandler", "MixedTargets", "NullDelegateInvoke",
    "NullInstanceTargetRejected", "NullTargetFaultsOnUse" })
{
    MethodInfo method = kernels.GetMethod(name)!;
    cases.Add(new { name, row = method.MetadataToken & 0xffffff, expected = method.Invoke(null, null)!.ToString() });
}
File.WriteAllText(Path.Combine(evidence, "oracle.json"), JsonSerializer.Serialize(new
{
    files, cases, runtime = Environment.Version.ToString(), process = RuntimeInformation.ProcessArchitecture.ToString()
}));
