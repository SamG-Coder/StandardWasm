using System.Runtime.InteropServices;
using System.Text.Json;

if (IntPtr.Size != 4) throw new InvalidOperationException("Use the Windows x86 CLR.");
var guest = typeof(Guest);
var cases = new[] { nameof(Guest.ClassSigned), nameof(Guest.ClassUnsigned), nameof(Guest.ValueControl),
    nameof(Guest.NullReceiver), nameof(Guest.NarrowClass), nameof(Guest.WideClass) }.Select(name =>
{
    var method = guest.GetMethod(name)!;
    int row = method.MetadataToken & 0xffffff;
    var rejection = name == nameof(Guest.NarrowClass)
        ? new { status = -2, location = new[] { 0, row, 6 } }
        : null;
    return new { name, row, expected = (int)method.Invoke(null, null)!, rejection };
});
File.WriteAllText(args[0], JsonSerializer.Serialize(new
{
    runtime = Environment.Version + "/" + RuntimeInformation.ProcessArchitecture, assembly = guest.Assembly.Location, cases
}));
