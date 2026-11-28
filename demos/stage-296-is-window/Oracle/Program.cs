using System.Runtime.InteropServices;
using System.Text.Json;

if (IntPtr.Size != 4) throw new InvalidOperationException("Expected the Windows x86 CLR.");

var results = new Dictionary<string, int>();
nint hidden = 0, visible = 0, child = 0;
try
{
    hidden = Native.MakeWindow(0, Native.HiddenDisabled);
    visible = Native.MakeWindow(0, Native.Visible);
    child = Native.MakeWindow(visible, Native.Child);
    if (Native.IsWindowVisible(hidden) != 0 || Native.IsWindowVisible(visible) == 0 ||
        Native.IsWindowVisible(child) == 0 || Native.IsWindowEnabled(hidden) != 0)
        throw new InvalidOperationException("Unexpected native fixture window state.");

    results["hiddenDisabled"] = Guest.Query(hidden);
    results["visible"] = Guest.Query(visible);
    results["child"] = Guest.Query(child);
    results["nullHandle"] = Guest.Query(0);
    results["invalidHandle"] = Guest.Query(-1);

    nint staleChild = child, staleTopLevel = hidden;
    Native.Destroy(ref child);
    Native.Destroy(ref hidden);
    results["staleChild"] = Guest.Query(staleChild);
    results["staleTopLevel"] = Guest.Query(staleTopLevel);
    results["remainingWindow"] = Guest.Query(visible);
}
finally
{
    Native.Destroy(ref child);
    Native.Destroy(ref hidden);
    Native.Destroy(ref visible);
}
File.WriteAllText(args[0], JsonSerializer.Serialize(new
{
    assembly = typeof(Guest).Assembly.Location,
    runtime = Environment.Version.ToString(),
    architecture = RuntimeInformation.ProcessArchitecture.ToString(),
    results
}));

internal static class Native
{
    internal const uint HiddenDisabled = 0x88000000, Visible = 0x90000000, Child = 0x50000000;
    private const uint NoActivateTool = 0x08000080;

    internal static nint MakeWindow(nint parent, uint style)
    {
        int position = parent == 0 ? -10_000 : 0;
        nint handle = CreateWindowExW(parent == 0 ? NoActivateTool : 0, "STATIC", "", style,
            position, position, 8, 8, parent, 0, 0, 0);
        if (handle == 0) throw new InvalidOperationException("CreateWindowExW failed.");
        return handle;
    }

    internal static void Destroy(ref nint handle)
    {
        if (handle == 0) return;
        if (DestroyWindow(handle) == 0) throw new InvalidOperationException("DestroyWindow failed.");
        handle = 0;
    }

    [DllImport("USER32.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
    private static extern nint CreateWindowExW(uint extendedStyle, string className, string title, uint style,
        int x, int y, int width, int height, nint parent, nint menu, nint instance, nint data);

    [DllImport("USER32.dll", ExactSpelling = true)]
    private static extern int DestroyWindow(nint handle);

    [DllImport("USER32.dll", ExactSpelling = true)]
    internal static extern int IsWindowVisible(nint handle);

    [DllImport("USER32.dll", ExactSpelling = true)]
    internal static extern int IsWindowEnabled(nint handle);
}
