using System.Runtime.InteropServices;
using Windows.Win32.Foundation;

public static unsafe class RectangleGuest
{
    public static int Window(nint hwnd, nint output) => GetWindowRect(hwnd, (RECT*)output);
    public static int Client(nint hwnd, nint output) => GetClientRect(hwnd, (RECT*)output);
    public static int SavedError() => Marshal.GetLastPInvokeError();
    public static nint ExecutableModule() => GetModuleHandleW(null);
    public static int Wrong(nint hwnd, nint address) => WrongPointee(hwnd, (int*)address);

    [DllImport("USER32.dll", ExactSpelling = true, SetLastError = true)]
    private static extern int GetWindowRect(nint hwnd, RECT* output);
    [DllImport("USER32.dll", ExactSpelling = true, SetLastError = true)]
    private static extern int GetClientRect(nint hwnd, RECT* output);
    [DllImport("USER32.dll", EntryPoint = "GetWindowRect", ExactSpelling = true, SetLastError = true)]
    private static extern int WrongPointee(nint hwnd, int* output);
    [DllImport("KERNEL32.dll", ExactSpelling = true)]
    private static extern nint GetModuleHandleW(char* name);
}

namespace Windows.Win32.Foundation
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct RECT
    {
        internal int left, top, right, bottom;
    }
}
