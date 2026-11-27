using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;

// Stage 155 (issue #269, decision 0082). Run on the 32-bit CLR: its runtime directory supplies the
// CoreLib the Wasm runner bundles, and its results are the expected values.
if (Environment.Is64BitProcess || IntPtr.Size != 4)
    throw new InvalidOperationException("Run the oracle on a 32-bit CLR.");
string evidence = args[0], fixture = Path.GetFullPath(args[1]);
string runtimeDirectory = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
var files = new[] { fixture, Path.Combine(runtimeDirectory, "System.Runtime.dll"),
    Path.Combine(runtimeDirectory, "System.Private.CoreLib.dll") };
Type kernels = Assembly.LoadFrom(fixture).GetType("BoundCatchFixture.Kernels", throwOnError: true)!;
var cases = new List<object>();
foreach (string name in new[] { "CaughtNotNull", "CaughtHResult", "LibraryClass", "BaseCatchOfDerived", "InnerOfBound",
    "DivideAsException", "NullAsException", "IndexAsException", "OverflowAsException", "RethrowFixedIdentity",
    "RethrowObjectIdentity", "NestedBound", "InitializerSameObject", "InitializerInner", "CastReachesBoundHandler" })
{
    MethodInfo method = kernels.GetMethod(name)!;
    string clr = method.Invoke(null, null)!.ToString()!;
    // A runtime InvalidCastException reaching a bound handler is the one explicit stop (decision 0082).
    string wasm = name == "CastReachesBoundHandler" ? "trap" : clr;
    cases.Add(new { name, row = method.MetadataToken & 0xffffff, clr, wasm });
}
File.WriteAllText(Path.Combine(evidence, "oracle.json"), JsonSerializer.Serialize(new
{
    files, cases, runtime = Environment.Version.ToString(), process = RuntimeInformation.ProcessArchitecture.ToString()
}));
