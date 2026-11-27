using System.Runtime.InteropServices;
using System.Text.Json;

if (IntPtr.Size != 4) throw new InvalidOperationException("Expected the Windows x86 CLR.");

var windows = new List<nint>();
var results = new Dictionary<string, int>();

nint Open(uint style, nint parent = 0)
{
    nint handle = Native.Create(style, parent);
    windows.Add(handle);
    return handle;
}

void Destroy(nint handle)
{
    if (Native.DestroyWindow(handle) == 0) throw new InvalidOperationException("DestroyWindow failed.");
    windows.Remove(handle);
}

try
{
    nint hidden = Open(Native.HiddenDisabled);
    nint visible = Open(Native.Visible);
    nint cover = Open(Native.Visible);
    Native.PlaceAbove(cover, visible);
    nint child = Open(Native.Child, visible);
    nint grandchild = Open(Native.Child, child);
    nint hiddenChild = Open(Native.HiddenChild, visible);
    nint childUnderHidden = Open(Native.Child, hidden);
    nint grandchildUnderHidden = Open(Native.Child, childUnderHidden);
    nint hiddenMiddle = Open(Native.Child, hiddenChild);
    nint ownedPopup = Open(Native.Visible, hidden);
    foreach (nint handle in new[] { child, grandchild, childUnderHidden, grandchildUnderHidden, hiddenMiddle, ownedPopup })
        if (!Native.HasVisibleStyle(handle)) throw new InvalidOperationException("Fixture lost the requested WS_VISIBLE style.");

    results["hidden"] = Guest.Query(hidden);
    results["covered"] = Guest.Query(visible);
    results["covering"] = Guest.Query(cover);
    results["child"] = Guest.Query(child);
    results["grandchild"] = Guest.Query(grandchild);
    results["hiddenChild"] = Guest.Query(hiddenChild);
    results["childUnderHidden"] = Guest.Query(childUnderHidden);
    results["grandchildUnderHidden"] = Guest.Query(grandchildUnderHidden);
    results["hiddenMiddle"] = Guest.Query(hiddenMiddle);
    results["ownedPopup"] = Guest.Query(ownedPopup);
    results["nullHandle"] = Guest.Query(0);
    results["invalidHandle"] = Guest.Query(-1);

    Destroy(grandchild);
    Destroy(cover);
    results["staleChild"] = Guest.Query(grandchild);
    results["staleTopLevel"] = Guest.Query(cover);
    results["remainingWindow"] = Guest.Query(visible);
}
finally
{
    // Children and owned windows are destroyed before their parents.
    for (int index = windows.Count - 1; index >= 0; index--) Native.DestroyWindow(windows[index]);
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
    internal const uint HiddenDisabled = 0x88000000, Visible = 0x90000000, Child = 0x50000000, HiddenChild = 0x40000000;
    private const uint NoActivateTool = 0x08000080;

    internal static nint Create(uint style, nint parent)
    {
        bool child = (style & HiddenChild) != 0;
        int position = child ? 0 : -10_000;
        nint handle = CreateWindowExW(child ? 0 : NoActivateTool, "STATIC", "", style,
            position, position, 8, 8, parent, 0, 0, 0);
        return handle == 0 ? throw new InvalidOperationException("CreateWindowExW failed.") : handle;
    }

    internal static void PlaceAbove(nint cover, nint covered)
    {
        // All top-level windows in this fixture share the same geometry.
        // Move the cover to the front without activating either window.
        const uint noMoveOrSizeOrActivate = 0x0013;
        if (SetWindowPos(cover, 0, 0, 0, 0, 0, noMoveOrSizeOrActivate) == 0)
            throw new InvalidOperationException("SetWindowPos failed.");
        for (nint window = GetWindow(cover, 2); window != 0; window = GetWindow(window, 2))
            if (window == covered) return;
        throw new InvalidOperationException("The covering window is not above its target.");
    }

    internal static bool HasVisibleStyle(nint handle) => (GetWindowLongW(handle, -16) & 0x10000000) != 0;

    [DllImport("USER32.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
    private static extern nint CreateWindowExW(uint extendedStyle, string className, string title, uint style,
        int x, int y, int width, int height, nint parent, nint menu, nint instance, nint data);

    [DllImport("USER32.dll", ExactSpelling = true)]
    internal static extern int DestroyWindow(nint handle);

    [DllImport("USER32.dll", ExactSpelling = true)]
    private static extern int SetWindowPos(nint handle, nint after, int x, int y, int width, int height, uint flags);

    [DllImport("USER32.dll", ExactSpelling = true)]
    private static extern nint GetWindow(nint handle, uint command);

    [DllImport("USER32.dll", ExactSpelling = true)]
    private static extern int GetWindowLongW(nint handle, int index);
}
