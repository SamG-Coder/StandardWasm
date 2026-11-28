using System.Runtime.InteropServices;
using System.Text.Json;

if (IntPtr.Size != 4) throw new InvalidOperationException("Use the Windows x86 CLR.");
var guest = typeof(Guest);
var cases = new[] { nameof(Guest.StringMembership), nameof(Guest.StringCastIdentity), nameof(Guest.Null),
    nameof(Guest.ClassAndPrimitiveBox), nameof(Guest.InvalidCast), nameof(Guest.StringAndArrayInterface) }.Select(name =>
{
    var method = guest.GetMethod(name)!;
    int row = method.MetadataToken & 0xffffff;
    bool overlap = name == nameof(Guest.StringAndArrayInterface);
    var rejection = overlap ? new { status = -2, location = new[] { 0, row, 1 } } : null;
    return new { name, row, expected = (int)method.Invoke(null, overlap ? new object[] { "string" } : null)!, rejection };
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
