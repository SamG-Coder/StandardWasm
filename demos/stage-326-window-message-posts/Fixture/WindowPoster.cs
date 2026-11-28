using System.Runtime.InteropServices;
using Windows.Win32.UI.WindowsAndMessaging;

public static unsafe class WindowPoster
{
    public static int PostWindow(nint hwnd, uint message, nuint wParam, nint lParam, int capture) =>
        capture == 0 ? PostRaw(hwnd, message, wParam, lParam) : PostMessageW(hwnd, message, wParam, lParam);

    public static int PeekWindow(nint address, nint hwnd, uint message, uint flags) =>
        PeekMessageW((MSG*)address, hwnd, message, message, flags);

    public static int RejectIntHandle() => WrongHandle(0, 0, 0, 0);

    [DllImport("USER32.dll", ExactSpelling = true, SetLastError = true)]
    private static extern int PostMessageW(nint hwnd, uint message, nuint wParam, nint lParam);
    [DllImport("USER32.dll", ExactSpelling = true, EntryPoint = "PostMessageW")]
    private static extern int PostRaw(nint hwnd, uint message, nuint wParam, nint lParam);
    [DllImport("USER32.dll", ExactSpelling = true, SetLastError = true)]
    private static extern int PeekMessageW(MSG* message, nint hwnd, uint minimum, uint maximum, uint flags);
    [DllImport("USER32.dll", ExactSpelling = true, EntryPoint = "PostMessageW")]
    private static extern int WrongHandle(int hwnd, uint message, nuint wParam, nint lParam);
}
