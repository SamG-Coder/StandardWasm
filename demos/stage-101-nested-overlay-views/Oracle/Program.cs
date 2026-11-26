using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Json;

var echo = typeof(DateOverlay).GetMethod(nameof(DateOverlay.Copy))!;
long[] inputs = [long.MinValue, -1, 0, 1, 0x123456789abcdef, long.MaxValue];
string core = typeof(DateTime).Assembly.Location;
File.WriteAllText(args[0], JsonSerializer.Serialize(new
{
    app = echo.DeclaringType!.Assembly.Location,
    facade = Path.Combine(Path.GetDirectoryName(core)!, "System.Runtime.dll"),
    core,
    runtime = RuntimeInformation.FrameworkDescription,
    entry = echo.MetadataToken & 0xffffff,
    inputs = inputs.Select(bits => bits.ToString(CultureInfo.InvariantCulture)),
    expected = inputs.Select(bits => DateOverlay.Copy(bits).ToString(CultureInfo.InvariantCulture))
}));

[StructLayout(LayoutKind.Explicit)]
public struct DateOverlay
{
    [FieldOffset(0)] public long Raw;
    [FieldOffset(0)] public DateTime Date;

    public static long Copy(long bits)
    {
        DateOverlay source = default;
        source.Raw = bits;
        DateTime date = source.Date;
        DateOverlay copy = default;
        copy.Date = date;
        return copy.Raw;
    }
}
