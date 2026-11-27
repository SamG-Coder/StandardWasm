using System.Runtime.InteropServices;
using System.Text.Json;

if (IntPtr.Size != 4) throw new InvalidOperationException("Expected Windows x86 CLR.");
var names = new Dictionary<nint, int> { [0] = 0 };
nint first = 0, second = 0, disabled = 0, child = 0;
try
{
    first = Make(1, 0, 0x80000000);
    second = Make(2, 0, 0x80000000);
    disabled = Make(3, 0, 0x88000000);
    child = Make(4, first, 0x40000000);
    int empty = Read();
    var firstSet = Pick(first);
    var repeated = Pick(first);
    var secondSet = Pick(second);
    var invalid = Pick(-1);
    int invalidError = Guest.SavedError();
    var disabledSet = Pick(disabled);
    var childSet = Pick(child);
    var zero = Pick(0);
    var restored = Pick(first);
    Destroy(ref child);
    Destroy(ref first);
    int destroyed = Read();

    File.WriteAllText(args[0], JsonSerializer.Serialize(new
    {
        assembly = typeof(Guest).Assembly.Location, runtime = Environment.Version.ToString(),
        outcomes = new { empty, firstSet, repeated, secondSet, invalid, invalidError, disabledSet, childSet, zero, restored, destroyed }
    }));
}
finally
{
    Destroy(ref child);
    Destroy(ref first);
    Destroy(ref disabled);
    Destroy(ref second);
}

int Read() => names.GetValueOrDefault(Guest.Query(), -1);
object Pick(nint window)
{
    int previous = names.GetValueOrDefault(Guest.Activate(window), -1);
    return new { previous, active = Read() };
}
nint Make(int key, nint parent, uint style)
{
    nint handle = CreateWindowExW(0, "STATIC", "", style, -10_000, -10_000, 8, 8, parent, 0, 0, 0);
    if (handle == 0) throw new InvalidOperationException("CreateWindowExW failed");
    names.Add(handle, key);
    return handle;
}
static void Destroy(ref nint handle)
{
    if (handle == 0) return;
    if (DestroyWindow(handle) == 0) throw new InvalidOperationException("DestroyWindow failed");
    handle = 0;
}
[DllImport("USER32.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
static extern nint CreateWindowExW(uint extendedStyle, string className, string title, uint style,
    int x, int y, int width, int height, nint parent, nint menu, nint instance, nint data);
[DllImport("USER32.dll", ExactSpelling = true)]
static extern int DestroyWindow(nint handle);
