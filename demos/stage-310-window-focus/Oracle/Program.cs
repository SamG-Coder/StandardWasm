using System.Runtime.InteropServices;
using System.Text.Json;

if (IntPtr.Size != 4) throw new InvalidOperationException("Expected Windows x86 CLR.");
var handles = new Dictionary<int, nint> { [0] = 0, [-1] = -1 };
var ids = new Dictionary<nint, int> { [0] = 0 };
var procedures = new Dictionary<nint, nint>();
var messages = new List<int[]>();
var observations = new List<object>();
WndProc callback = Deliver;
try
{
    Make(1, 0, 0x80000000);
    Make(2, 0, 0x80000000);
    Make(3, 0, 0x88000000);
    Make(4, 1, 0x40000000);
    Make(5, 4, 0x40000000);

    Observe("query", 0);
    Observe("activate", 1);
    Observe("focus", 4);
    Observe("focus", 4);
    Observe("activate", 1);
    Observe("focus", 5);
    Observe("destroy", 5);
    Observe("focus", 2);
    Observe("focus", 3, checkError: true);
    Observe("focus", -1, checkError: true);
    Observe("focus", 0);
    Observe("focus", 0);
    Observe("activate", 2);
    Observe("focus", 4);
    Observe("disable", 4);
    Observe("focus", 4, checkError: true);
    Observe("enable", 4);
    Observe("focus", 4);
    Observe("disable", 1);
    Observe("focus", 4, checkError: true);
    Observe("enable", 1);
    Observe("focus", 4);
    Observe("destroy", 4);
    Observe("destroy", 1);

    File.WriteAllText(args[0], JsonSerializer.Serialize(new
    {
        assembly = typeof(Guest).Assembly.Location, runtime = Environment.Version.ToString(), observations
    }));
}
finally
{
    foreach (int id in new[] { 5, 4, 3, 2, 1 })
        if (handles.ContainsKey(id)) Destroy(id);
    GC.KeepAlive(callback);
}

int Id(nint handle) => ids.GetValueOrDefault(handle, -1);

void Observe(string operation, int id, bool checkError = false)
{
    int previous = operation switch
    {
        "activate" => Id(Guest.Activate(handles[id])),
        "focus" => Id(Guest.Pick(handles[id])),
        "enable" => Guest.Enable(handles[id], 1),
        "disable" => Guest.Enable(handles[id], 0),
        "destroy" => Destroy(id),
        "query" => 0,
        _ => throw new InvalidOperationException(operation)
    };
    int active = Id(Guest.Active()), focus = Id(Guest.Focus());
    int? error = checkError ? Guest.SavedError() : null;
    observations.Add(new { operation, id, previous, active, focus, error, messages = messages.ToArray() });
    messages.Clear();
}

void Make(int id, int parent, uint style)
{
    nint handle = CreateWindowExW(0, "STATIC", "", style, -10_000, -10_000, 8, 8, handles[parent], 0, 0, 0);
    if (handle == 0) throw new InvalidOperationException("CreateWindowExW failed.");
    handles.Add(id, handle);
    ids.Add(handle, id);
    nint previous = SetWindowLongW(handle, -4, Marshal.GetFunctionPointerForDelegate(callback));
    if (previous == 0) throw new InvalidOperationException("Could not observe the focus messages.");
    procedures.Add(handle, previous);
}

int Destroy(int id)
{
    if (DestroyWindow(handles[id]) == 0) throw new InvalidOperationException("DestroyWindow failed.");
    handles.Remove(id);
    return 0;
}

nint Deliver(nint window, uint message, nint wparam, nint lparam)
{
    if (message is 7 or 8)
        messages.Add([Id(window), (int)message, Id(wparam), Id(Guest.Active()), Id(Guest.Focus())]);
    return CallWindowProcW(procedures[window], window, message, wparam, lparam);
}

[DllImport("USER32.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
static extern nint CreateWindowExW(uint extendedStyle, string className, string title, uint style,
    int x, int y, int width, int height, nint parent, nint menu, nint instance, nint data);
[DllImport("USER32.dll", ExactSpelling = true)]
static extern int DestroyWindow(nint handle);
[DllImport("USER32.dll", ExactSpelling = true)]
static extern nint SetWindowLongW(nint window, int index, nint value);
[DllImport("USER32.dll", ExactSpelling = true)]
static extern nint CallWindowProcW(nint procedure, nint window, uint message, nint wparam, nint lparam);

[UnmanagedFunctionPointer(CallingConvention.StdCall)]
delegate nint WndProc(nint window, uint message, nint wparam, nint lparam);
