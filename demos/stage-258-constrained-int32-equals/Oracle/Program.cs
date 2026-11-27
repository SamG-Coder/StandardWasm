using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;

if (IntPtr.Size != 4) throw new InvalidOperationException("Use the Windows x86 CLR.");
var guest = typeof(Probe);
var reference = guest.GetMethod("EqualReference", BindingFlags.NonPublic | BindingFlags.Static)!;
int referenceRow = reference.MetadataToken & 0xffffff;
var cases = new[] { nameof(Probe.SignedValues), nameof(Probe.BoxIdentity), nameof(Probe.LiveFieldReceiver),
    nameof(Probe.NullLocationMismatch), nameof(Probe.NullLocationMatch), nameof(Probe.DirectAndConstrained),
    nameof(Probe.UnsignedBoundary) }.Select(name =>
{
    var method = guest.GetMethod(name)!;
    int row = method.MetadataToken & 0xffffff;
    var rejection = name == nameof(Probe.UnsignedBoundary)
        ? new { status = -2, location = new[] { 0, referenceRow, 2 } }
        : null;
    return new { name, row, expected = (int)method.Invoke(null, null)!, rejection };
});
string coreLib = typeof(object).Assembly.Location;
string runtime = Path.Combine(Path.GetDirectoryName(coreLib)!, "System.Runtime.dll");
File.WriteAllText(args[0], JsonSerializer.Serialize(new
{
    runtime = Environment.Version + "/" + RuntimeInformation.ProcessArchitecture,
    assembly = guest.Assembly.Location,
    dependencies = new[] { coreLib, runtime },
    cases
}));
