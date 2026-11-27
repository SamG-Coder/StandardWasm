using System.Runtime.InteropServices;
using Windows.Win32.Foundation;

public static unsafe class Guest
{
    public static nint ExecutableModule() => GetModuleHandleW(null);

    public static int Invalidate(nint hwnd, nint rectangle, int erase) =>
        InvalidateRect(new HWND(hwnd), (RECT*)rectangle, new BOOL(erase)).Value;

    public static int UndersizedPointee(nint hwnd, nint rectangle) =>
        WrongPointer(new HWND(hwnd), (int*)rectangle, new BOOL(0)).Value;

    public static int Error() => Marshal.GetLastSystemError();
    public static int SavedError() => Marshal.GetLastPInvokeError();

    public static void SetErrors(int system, int saved)
    {
        Marshal.SetLastPInvokeError(saved);
        Marshal.SetLastSystemError(system);
    }

    [DllImport("USER32.dll", ExactSpelling = true)]
    private static extern BOOL InvalidateRect(HWND hwnd, RECT* rectangle, BOOL erase);

    [DllImport("USER32.dll", EntryPoint = "InvalidateRect", ExactSpelling = true)]
    private static extern BOOL WrongPointer(HWND hwnd, int* rectangle, BOOL erase);

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

    internal readonly struct BOOL
    {
        internal readonly int Value;
        internal BOOL(int value) => Value = value;
    }

#pragma warning disable CS0649 // Native ABI payload.
    internal struct RECT
    {
        internal int Left, Top, Right, Bottom;
    }
#pragma warning restore CS0649
}
