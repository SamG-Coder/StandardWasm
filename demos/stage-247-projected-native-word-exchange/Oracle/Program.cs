using System.Runtime.InteropServices;
using System.Text.Json;

if (IntPtr.Size != 4) throw new InvalidOperationException("Use the Windows x86 CLR.");
var coreLib = typeof(object).Assembly.Location;
string folder = Path.GetDirectoryName(coreLib)!;
var dependencies = new[] { coreLib, Path.Combine(folder, "System.Runtime.dll"), Path.Combine(folder, "System.Threading.dll") };
var guest = typeof(Guest);
var cases = new[] { nameof(Guest.Projected), nameof(Guest.StableAlias), nameof(Guest.Distinct), nameof(Guest.Frame), nameof(Guest.NullLocation) }
    .Select(name =>
    {
        var method = guest.GetMethod(name)!;
        return new { name, row = method.MetadataToken & 0xffffff, expected = (int)method.Invoke(null, null)! };
    });
File.WriteAllText(args[0], JsonSerializer.Serialize(new
{
    runtime = Environment.Version + "/" + RuntimeInformation.ProcessArchitecture, assembly = guest.Assembly.Location, dependencies, cases
}));
