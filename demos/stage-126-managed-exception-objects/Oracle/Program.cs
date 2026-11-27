using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;

// Stage 126 (decision 0054). Run on the 32-bit CLR: its runtime directory is the
// supported CoreLib pack (decision 0044), and its results are the expected values.
if (Environment.Is64BitProcess || IntPtr.Size != 4)
    throw new InvalidOperationException("Run the oracle on a 32-bit CLR.");
string evidence = args[0], fixture = Path.GetFullPath(args[1]), twin = Path.GetFullPath(args[2]);
string runtimeDirectory = Path.GetDirectoryName(typeof(object).Assembly.Location)!;

var files = new List<string> { fixture, twin,
    Path.Combine(runtimeDirectory, "System.Runtime.dll"), Path.Combine(runtimeDirectory, "System.Private.CoreLib.dll") };
int[] images = [0, 1, 2, 3];

Type kernels = Assembly.LoadFrom(fixture).GetType("ExceptionFixture.Kernels", throwOnError: true)!;
var cases = new List<object>();
foreach (string name in new[]
{
    "TypedCatch", "DerivedBeforeBase", "BaseCatchesDerived", "MismatchReachesOuter", "CatchException",
    "RethrowKeepsType", "FinallyOrder", "NullVariableThrow", "RepeatedHandlerVisit", "SameNameStaysDistinct", "DefaultMessageExceptions"
})
{
    MethodInfo method = kernels.GetMethod(name)!;
    int value = (int)method.Invoke(null, null)!;
    cases.Add(new { name, row = Row(method), images, clr = value.ToString(), wasm = value.ToString() });
}

// Exception filters keep the explicit diagnostic at their handler (decision 0054).
MethodInfo filtered = kernels.GetMethod("Filtered")!;
int filteredValue = (int)filtered.Invoke(null, null)!;
ExceptionHandlingClause filter = filtered.GetMethodBody()!.ExceptionHandlingClauses
    .Single(clause => clause.Flags == ExceptionHandlingClauseOptions.Filter);
cases.Add(new
{
    name = "Filtered", row = Row(filtered), images, clr = filteredValue.ToString(),
    status = -2, diagnostic = new[] { 0, Row(filtered), filter.HandlerOffset }
});

// An object exception nothing catches reaches the host on the exported object tag.
MethodInfo uncaught = kernels.GetMethod("Uncaught")!;
string thrown;
try { uncaught.Invoke(null, null); thrown = "none"; }
catch (TargetInvocationException error) { thrown = error.InnerException!.GetType().FullName!; }
if (thrown != "ExceptionFixture.FixtureException")
    throw new InvalidOperationException("The CLR did not throw the fixture exception: " + thrown);
cases.Add(new { name = "Uncaught", row = Row(uncaught), images, clr = thrown, uncaught = true });

// throw; preserves the original object: the uncaught payload must be the very reference Saved() returns.
MethodInfo rethrow = kernels.GetMethod("UncaughtRethrow")!, saved = kernels.GetMethod("Saved")!;
object? original = null;
try { rethrow.Invoke(null, null); }
catch (TargetInvocationException error) { original = error.InnerException; }
if (original == null || !ReferenceEquals(original, saved.Invoke(null, null)))
    throw new InvalidOperationException("The CLR rethrow did not preserve the original exception object.");
cases.Add(new
{
    name = "UncaughtRethrow", row = Row(rethrow), images, clr = "same object", uncaught = true,
    identity = new { name = "saved", row = Row(saved) }
});

File.WriteAllText(Path.Combine(evidence, "oracle.json"), JsonSerializer.Serialize(new
{
    files, cases, runtime = Environment.Version.ToString(), process = RuntimeInformation.ProcessArchitecture.ToString()
}));

static int Row(MethodInfo method) => method.MetadataToken & 0xffffff;
