using System.Runtime.InteropServices;
using System.Text.Json;

if (IntPtr.Size != 4) throw new InvalidOperationException("Use the Windows x86 CLR.");
var guest = typeof(Probe);
var cases = new[] { nameof(Probe.SignedLocal), nameof(Probe.UnsignedLocal), nameof(Probe.ArrayAlias),
    nameof(Probe.ClassFieldAlias), nameof(Probe.AggregateFieldAlias), nameof(Probe.NullAddress),
    nameof(Probe.BooleanAndReferenceControls) }.Select(name =>
{
    var method = guest.GetMethod(name)!;
    int row = method.MetadataToken & 0xffffff;
    return new { name, row, expected = (int)method.Invoke(null, null)! };
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
