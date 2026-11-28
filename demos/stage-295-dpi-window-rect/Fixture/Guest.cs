using System.Runtime.InteropServices;
using Windows.Win32.Foundation;

public static unsafe class Guest
{
    public static nint ExecutableModule() => GetModuleHandleW(null);

    public static int Adjust(nint address, int style, int menu, int extendedStyle, int dpi) =>
        AdjustWindowRectExForDpi((RECT*)address, (uint)style, menu, (uint)extendedStyle, (uint)dpi);

    public static int RejectWrongPointee(nint address) => WrongPointee((int*)address, 0, 0, 0, 96);

    [DllImport("KERNEL32.dll", ExactSpelling = true)]
    private static extern nint GetModuleHandleW(char* name);

    [DllImport("USER32.dll", ExactSpelling = true)]
    private static extern int AdjustWindowRectExForDpi(RECT* rectangle, uint style, int menu, uint extendedStyle, uint dpi);

    [DllImport("USER32.dll", EntryPoint = "AdjustWindowRectExForDpi", ExactSpelling = true)]
    private static extern int WrongPointee(int* rectangle, uint style, int menu, uint extendedStyle, uint dpi);
}

namespace Windows.Win32.Foundation
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct RECT
    {
        internal int Left, Top, Right, Bottom;
    }
}
