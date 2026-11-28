using System.Runtime.InteropServices;
using Windows.Win32.UI.WindowsAndMessaging;

public static unsafe class Guest
{
    public static uint ThreadId() => GetCurrentThreadId();
    public static nint ExecutableModule() => GetModuleHandleW(null);
    public static int SavedError() => Marshal.GetLastPInvokeError();
    public static void SetSavedError(int error) => Marshal.SetLastPInvokeError(error);

    public static int Post(uint thread, uint message, nuint wParam, nint lParam, int capture) =>
        capture == 0 ? PostThreadMessageW(thread, message, wParam, lParam) : PostCaptured(thread, message, wParam, lParam);

    public static int Peek(nint address, nint window, uint minimum, uint maximum, uint flags) =>
        PeekMessageW((MSG*)address, window, minimum, maximum, flags);

    public static int RejectWrongPointee(nint address) => WrongPointee((int*)address, 0, 0, 0, 0);

    [DllImport("KERNEL32.dll", ExactSpelling = true)]
    private static extern uint GetCurrentThreadId();
    [DllImport("KERNEL32.dll", ExactSpelling = true)]
    private static extern nint GetModuleHandleW(char* name);
    [DllImport("USER32.dll", ExactSpelling = true)]
    private static extern int PostThreadMessageW(uint thread, uint message, nuint wParam, nint lParam);
    [DllImport("USER32.dll", EntryPoint = "PostThreadMessageW", ExactSpelling = true, SetLastError = true)]
    private static extern int PostCaptured(uint thread, uint message, nuint wParam, nint lParam);
    [DllImport("USER32.dll", ExactSpelling = true)]
    private static extern int PeekMessageW(MSG* message, nint window, uint minimum, uint maximum, uint flags);
    [DllImport("USER32.dll", EntryPoint = "PeekMessageW", ExactSpelling = true)]
    private static extern int WrongPointee(int* message, nint window, uint minimum, uint maximum, uint flags);
}

namespace Windows.Win32.UI.WindowsAndMessaging
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct MSG
    {
        internal nint hwnd;
        internal uint message;
        internal nuint wParam;
        internal nint lParam;
        internal uint time;
        internal Point pt;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct Point
    {
        internal int x, y;
    }
}
