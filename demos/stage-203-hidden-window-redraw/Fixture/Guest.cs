using System.Runtime.InteropServices;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;

public static unsafe class Guest
{
    public static nint ExecutableModule() => GetModuleHandleW(null);

    public static int Redraw(nint hwnd, nint rectangle, nint region, int flags) =>
        RedrawWindow(new HWND(hwnd), (RECT*)rectangle, new HRGN(region), (REDRAW_WINDOW_FLAGS)flags).Value;

    public static int Error() => Marshal.GetLastSystemError();

    public static void SetErrors(int system, int saved)
    {
        Marshal.SetLastPInvokeError(saved);
        Marshal.SetLastSystemError(system);
    }

    public static int SavedError() => Marshal.GetLastPInvokeError();

    [DllImport("USER32.dll", ExactSpelling = true)]
    private static extern BOOL RedrawWindow(HWND hwnd, RECT* rectangle, HRGN region, REDRAW_WINDOW_FLAGS flags);

    [DllImport("KERNEL32.dll", ExactSpelling = true)]
    private static extern nint GetModuleHandleW(char* name);
}

namespace Windows.Win32.Foundation
{
    internal readonly struct HWND
    {
        private readonly nint value;
        internal HWND(nint value) => this.value = value;
    }

#pragma warning disable CS0649 // Native ABI payloads are populated by Windows.
    internal readonly struct BOOL
    {
        internal readonly int Value;
    }

    internal struct RECT
    {
        internal int Left, Top, Right, Bottom;
    }
#pragma warning restore CS0649
}

namespace Windows.Win32.Graphics.Gdi
{
    internal readonly struct HRGN
    {
        private readonly nint value;
        internal HRGN(nint value) => this.value = value;
    }

    internal enum REDRAW_WINDOW_FLAGS : uint { Invalidate = 1 }
}
