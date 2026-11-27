using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;

if (IntPtr.Size != 4) throw new InvalidOperationException("Use the Windows x86 CLR.");
var guest = typeof(Probe);
var mixed = guest.GetMethod("ReadMixed", BindingFlags.NonPublic | BindingFlags.Static)!;
var nominal = guest.GetMethod("ReadNominal", BindingFlags.NonPublic | BindingFlags.Static)!;
var delegator = new TypeDelegator(typeof(long));
if ((int)nominal.Invoke(null, [delegator])! != 113 || (int)mixed.Invoke(null, [delegator])! != 127)
    throw new InvalidOperationException("Expected the CLR to support TypeDelegator in either Type[] shape.");
int mixedRow = mixed.MetadataToken & 0xffffff;
var cases = new[] { nameof(Probe.DescriptorCells), nameof(Probe.SharedIdentity), nameof(Probe.NullCell),
    nameof(Probe.Bounds), nameof(Probe.NominalPath), nameof(Probe.MixedPath), nameof(Probe.AddressedLocal) }.Select(name =>
{
    var method = guest.GetMethod(name)!;
    int row = method.MetadataToken & 0xffffff;
    var rejection = name switch
    {
        nameof(Probe.MixedPath) => new { status = -1, location = new[] { 0, mixedRow, 22 } },
        nameof(Probe.AddressedLocal) => new { status = -1, location = new[] { 0, row, 19 } },
        _ => null
    };
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
