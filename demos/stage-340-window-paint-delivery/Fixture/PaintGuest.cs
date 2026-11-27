using System.Runtime.InteropServices;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;

// Mirrors the WinForms SDK shapes: HWND, HDC and BOOL handle structs,
// RECT and the 64-byte x86 PAINTSTRUCT passed by pointer.
public static unsafe class PaintGuest
{
    public static int Invalidate(nint hwnd, nint rectangle, int erase) =>
        InvalidateRect(new HWND(hwnd), (RECT*)rectangle, new BOOL(erase)).Value;
    public static int Validate(nint hwnd, nint rectangle) => ValidateRect(new HWND(hwnd), (RECT*)rectangle).Value;
    public static int UpdateRect(nint hwnd, nint rectangle, int erase) =>
        GetUpdateRect(new HWND(hwnd), (RECT*)rectangle, new BOOL(erase)).Value;
    public static nint Begin(nint hwnd, nint paint) => BeginPaint(new HWND(hwnd), (PAINTSTRUCT*)paint).Value;
    public static int End(nint hwnd, nint paint) => EndPaint(new HWND(hwnd), (PAINTSTRUCT*)paint).Value;
    public static nint RejectPaintPointee(nint hwnd, nint address) => WrongPaintPointee(new HWND(hwnd), (int*)address).Value;
    public static int SavedError() => Marshal.GetLastPInvokeError();
    public static int SystemError() => Marshal.GetLastSystemError();
    public static void SetErrors(int error)
    {
        Marshal.SetLastPInvokeError(error);
        Marshal.SetLastSystemError(error);
    }

    [DllImport("USER32.dll", ExactSpelling = true)]
    private static extern BOOL InvalidateRect(HWND hwnd, RECT* rectangle, BOOL erase);
    [DllImport("USER32.dll", ExactSpelling = true)]
    private static extern BOOL ValidateRect(HWND hwnd, RECT* rectangle);
    [DllImport("USER32.dll", ExactSpelling = true)]
    private static extern BOOL GetUpdateRect(HWND hwnd, RECT* rectangle, BOOL erase);
    [DllImport("USER32.dll", ExactSpelling = true)]
    private static extern HDC BeginPaint(HWND hwnd, PAINTSTRUCT* paint);
    [DllImport("USER32.dll", ExactSpelling = true)]
    private static extern BOOL EndPaint(HWND hwnd, PAINTSTRUCT* paint);
    [DllImport("USER32.dll", EntryPoint = "BeginPaint", ExactSpelling = true)]
    private static extern HDC WrongPaintPointee(HWND hwnd, int* paint);
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
        private readonly int value;
        internal BOOL(int value) => this.value = value;
        internal int Value => value;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct RECT
    {
        internal int left, top, right, bottom;
    }
}

namespace Windows.Win32.Graphics.Gdi
{
    internal readonly struct HDC
    {
        private readonly nint value;
        internal nint Value => value;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct PAINTSTRUCT
    {
        internal HDC hdc;
        internal Windows.Win32.Foundation.BOOL fErase;
        internal Windows.Win32.Foundation.RECT rcPaint;
        internal Windows.Win32.Foundation.BOOL fRestore;
        internal Windows.Win32.Foundation.BOOL fIncUpdate;
        internal fixed byte rgbReserved[32];
    }
}
