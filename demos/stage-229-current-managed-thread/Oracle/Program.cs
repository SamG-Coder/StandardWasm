using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;

if (IntPtr.Size != 4)
    throw new InvalidOperationException("Expected Windows x86 CLR.");

var tests = typeof(CurrentThreadTests);
var runtimeType = typeof(Thread);
string coreLib = runtimeType.Assembly.Location;
string runtimeFolder = Path.GetDirectoryName(coreLib)!;
var dependencies = new[]
{
    coreLib,
    Path.Combine(runtimeFolder, "System.Runtime.dll"),
    Path.Combine(runtimeFolder, "System.Threading.Thread.dll"),
    Path.Combine(runtimeFolder, "System.Threading.dll")
};

var stateLeaf = runtimeType.GetMethod("GetThreadState", BindingFlags.Static | BindingFlags.NonPublic)!;
var cases = new[]
{
    nameof(CurrentThreadTests.Identity), nameof(CurrentThreadTests.ManagedId),
    nameof(CurrentThreadTests.Context), nameof(CurrentThreadTests.Priority),
    nameof(CurrentThreadTests.Reference), nameof(CurrentThreadTests.NativeState)
}.Select(name =>
{
    var method = tests.GetMethod(name)!;
    bool referenceIdentity = name == nameof(CurrentThreadTests.Reference);
    object actual = method.Invoke(null, null)!;
    int expected = referenceIdentity ? ReferenceEquals(actual, Thread.CurrentThread) ? 1 : 0 : (int)actual;
    var rejection = name == nameof(CurrentThreadTests.NativeState)
        ? new { status = -2, location = new[] { 1, stateLeaf.MetadataToken & 0xffffff, -1 } }
        : null;
    return new { name, row = method.MetadataToken & 0xffffff, expected, referenceIdentity, rejection };
});

File.WriteAllText(args[0], JsonSerializer.Serialize(new
{
    runtime = Environment.Version + "/" + RuntimeInformation.ProcessArchitecture,
    assembly = tests.Assembly.Location,
    dependencies,
    cases
}));
