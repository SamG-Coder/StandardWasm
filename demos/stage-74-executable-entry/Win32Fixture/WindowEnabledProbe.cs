using System.Runtime.InteropServices;
using Windows.Win32.Foundation;

namespace Windows.Win32.Foundation
{
    internal unsafe struct HWND
    {
        internal readonly void* Value;
        internal HWND(void* value) => Value = value;
    }
}

public static unsafe class WindowEnabledProbe
{
    [DllImport("USER32.dll", ExactSpelling = true)]
    private static extern BOOL IsWindowEnabled(HWND window);

    [DllImport("USER32.dll", ExactSpelling = true)]
    private static extern BOOL EnableWindow(HWND window, BOOL enabled);

    public static int WriteWindowEnabled(nint handle, int enabled) =>
        EnableWindow(new HWND((void*)handle), new BOOL(enabled)).Value != 0 ? 1 : 0;

    public static int ReadWindowEnabled(nint handle) => IsWindowEnabled(new HWND((void*)handle)).Value != 0 ? 1 : 0;
}
