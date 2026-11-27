using System.Runtime.InteropServices;

namespace SendMessageTimeoutFixture;

public static unsafe class WindowSender
{
    [DllImport("USER32.dll", EntryPoint = "SendMessageTimeoutW", ExactSpelling = true)]
    private static extern nint Send(nint window, uint message, nuint wParam, nint lParam, uint flags, uint timeout, nuint* result);

    [DllImport("USER32.dll", EntryPoint = "SendMessageTimeoutW", ExactSpelling = true, SetLastError = true)]
    private static extern nint SendAndCapture(nint window, uint message, nuint wParam, nint lParam, uint flags, uint timeout, nuint* result);

    [DllImport("USER32.dll", EntryPoint = "SendMessageTimeoutW", ExactSpelling = true)]
    private static extern nint WrongResult(nint window, uint message, nuint wParam, nint lParam, uint flags, uint timeout, int* result);

    [DllImport("KERNEL32.dll", ExactSpelling = true)]
    private static extern nint GetModuleHandleW(char* name);

    public static nint ExecutableModule() => GetModuleHandleW(null);

    public static int SendWindow(nint window, int message, nint wParam, nint lParam, int flags, int timeout, nint result, int capture)
    {
        if (capture != 0)
            return (int)SendAndCapture(window, (uint)message, (nuint)wParam, lParam, (uint)flags, (uint)timeout, (nuint*)result);
        return (int)Send(window, (uint)message, (nuint)wParam, lParam, (uint)flags, (uint)timeout, (nuint*)result);
    }

    public static void SetErrors(int system, int pinvoke)
    {
        Marshal.SetLastSystemError(system);
        Marshal.SetLastPInvokeError(pinvoke);
    }

    public static int SystemError() => Marshal.GetLastSystemError();
    public static int SavedError() => Marshal.GetLastPInvokeError();

    public static int RejectWrongResult(nint window, nint pointer) =>
        (int)WrongResult(window, 0x8042, 0, 0, 0, 1, (int*)pointer);
}
