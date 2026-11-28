using System.Runtime.InteropServices;
using System.Text.Json;

if (!OperatingSystem.IsWindows() || IntPtr.Size != 4)
    throw new InvalidOperationException("Windows x86 is required.");

const uint message = 0x8051;
var windows = new Dictionary<string, nint>
{
    ["A"] = Open(0, -3), ["B"] = Open(0, -3)
};
windows["child"] = Open(0x40000000, windows["A"]);
var operations = new List<object>();
var retired = new HashSet<string>();
try
{
    Post("A", message, 11, 1);
    Post("zero", message, 21, 1);
    PostThread(25);
    Post("B", message, 0x80000001, 0);
    Post("invalid", message, 41, 1);
    Post("A", 0x000c, 0, 1);
    Peek("B", 0);
    Peek("invalid", 1);
    Peek("thread", 1);
    Peek("thread", 1);
    Peek("zero", 1);
    Peek("zero", 1);
    Peek("zero", 1);
    Post("child", message, 61, 1);
    Peek("A", 1);
    Post("B", message, 71, 1);
    Destroy("B");
    Peek("zero", 1);
    Post("B", message, 81, 1);
    File.WriteAllText(args[0], JsonSerializer.Serialize(new { assembly = typeof(WindowPoster).Assembly.Location,
        clr = Environment.Version.ToString(), message, operations }));
}
finally
{
    foreach (string name in new[] { "child", "A", "B" })
        if (!retired.Contains(name)) DestroyWindow(windows[name]);
}

nint Handle(string name) => name switch { "zero" => 0, "thread" => -1, "invalid" => -99, _ => windows[name] };

void Post(string target, uint msg, uint payload, int capture)
{
    Guest.SetSavedError(789);
    int result = WindowPoster.PostWindow(Handle(target), msg, payload, -100, capture);
    operations.Add(new { kind = "post", target, msg, payload, capture, expected = new { result, error = Guest.SavedError() } });
}

void PostThread(uint payload)
{
    int result = Guest.Post(Guest.ThreadId(), message, payload, -100, 1);
    operations.Add(new { kind = "post-thread", payload, expected = new { result, error = Guest.SavedError() } });
}

unsafe void Peek(string filter, uint flags)
{
    int* words = stackalloc int[8];
    for (int i = 0; i < 8; i++) words[i] = -1;
    int result = WindowPoster.PeekWindow((nint)words, Handle(filter), message, flags);
    int error = Guest.SavedError();
    int hwnd = words[0];
    string target = hwnd == 0 ? "zero" : hwnd == -1 ? "unchanged" : windows.Single(entry => entry.Value == hwnd).Key;
    operations.Add(new { kind = "peek", filter, flags, expected = new { result, error, target,
        msg = (uint)words[1], payload = (uint)words[2], lParam = words[3], guard = words[7] } });
}

void Destroy(string target)
{
    int result = DestroyWindow(Handle(target));
    if (result == 0) throw new InvalidOperationException("DestroyWindow failed.");
    retired.Add(target);
    operations.Add(new { kind = "destroy", target, expected = new { result } });
}

static nint Open(uint style, nint parent)
{
    nint hwnd = CreateWindowExW(0, "STATIC", "", style, 0, 0, 0, 0, parent, 0, 0, 0);
    if (hwnd == 0) throw new InvalidOperationException("Could not open the test window.");
    return hwnd;
}

[DllImport("USER32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
static extern nint CreateWindowExW(uint extended, string type, string title, uint style, int x, int y,
    int width, int height, nint parent, nint menu, nint module, nint param);
[DllImport("USER32.dll", ExactSpelling = true)]
static extern int DestroyWindow(nint hwnd);
