using System.Runtime.InteropServices;
using System.Text.Json;

if (IntPtr.Size != 4) throw new InvalidOperationException("Expected the Windows x86 CLR.");

var handles = new Dictionary<nint, string>();
var observations = new Dictionary<string, object>();
uint thread = Native.GetCurrentThreadId();
// Suppress the system IME helper so the oracle enumerates only fixture windows.
if (Native.ImmDisableIME(thread) == 0) throw new InvalidOperationException("ImmDisableIME failed.");
const int payload = unchecked((int)0xd1234567);

void Observe(string name, uint owner, int stopAfter)
{
    int result = Guest.Query(owner, payload, stopAfter);
    var visited = new string[Guest.Count()];
    for (int index = 0; index < visited.Length; index++) visited[index] = handles[Guest.HandleAt(index)];
    observations.Add(name, new { result, visited, payload = (int)Guest.Payload() });
}

nint Create(string name, uint style, nint parent = 0)
{
    nint handle = Native.CreateWindowExW(0x08000080, "STATIC", "", style, -10000, -10000, 8, 8, parent, 0, 0, 0);
    if (handle == 0) throw new InvalidOperationException("CreateWindowExW failed.");
    handles.Add(handle, name);
    return handle;
}

try
{
    Observe("empty", thread, 4);
    nint first = Create("first", 0x80000000);
    Create("child", 0x40000000, first);
    Create("second", 0x80000000);
    Create("third", 0x80000000);
    Create("message", 0, -3);
    Observe("all", thread, 4);
    Observe("stopFirst", thread, 1);
    Observe("stopSecond", thread, 2);
    Observe("invalidThread", uint.MaxValue, 4);
    Native.DestroyWindow(first);
    handles.Remove(first);
    Observe("destroyedWindows", thread, 4);
}
finally
{
    foreach (nint handle in handles.Keys.Reverse()) Native.DestroyWindow(handle);
}

File.WriteAllText(args[0], JsonSerializer.Serialize(new
{
    assembly = typeof(Guest).Assembly.Location,
    runtime = Environment.Version.ToString(),
    architecture = RuntimeInformation.ProcessArchitecture.ToString(),
    os = Environment.OSVersion.VersionString,
    observations
}));

internal static class Native
{
    [DllImport("IMM32.dll", ExactSpelling = true)]
    internal static extern int ImmDisableIME(uint thread);

    [DllImport("KERNEL32.dll", ExactSpelling = true)]
    internal static extern uint GetCurrentThreadId();

    [DllImport("USER32.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
    internal static extern nint CreateWindowExW(uint extendedStyle, string className, string title, uint style,
        int x, int y, int width, int height, nint parent, nint menu, nint instance, nint data);

    [DllImport("USER32.dll", ExactSpelling = true)]
    internal static extern int DestroyWindow(nint handle);
}
