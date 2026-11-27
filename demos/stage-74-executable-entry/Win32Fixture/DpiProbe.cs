using System.Runtime.InteropServices;
using Windows.Win32.Foundation;
using Windows.Win32.UI.HiDpi;

namespace Windows.Win32.Foundation
{
    internal struct HRESULT
    {
        internal readonly int Value;

        internal HRESULT(int value) => Value = value;
    }
}

namespace Windows.Win32.UI.HiDpi
{
    internal unsafe struct DPI_AWARENESS_CONTEXT
    {
        internal static readonly DPI_AWARENESS_CONTEXT Unspecified = new DPI_AWARENESS_CONTEXT(0);
        internal readonly void* Value;
        internal static readonly DPI_AWARENESS_CONTEXT Unaware = new DPI_AWARENESS_CONTEXT(-1);

        internal DPI_AWARENESS_CONTEXT(int value) => Value = (void*)(nint)value;
    }
}

public static class DpiProbe
{
    [DllImport("USER32.dll", ExactSpelling = true)]
    private static extern BOOL IsValidDpiAwarenessContext(DPI_AWARENESS_CONTEXT value);

    [DllImport("USER32.dll", ExactSpelling = true)]
    private static extern BOOL SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT value);

    [DllImport("USER32.dll", ExactSpelling = true)]
    private static extern BOOL SetProcessDPIAware();

    [DllImport("api-ms-win-shcore-scaling-l1-1-1.dll", ExactSpelling = true)]
    private static extern HRESULT SetProcessDpiAwareness(int value);

    [DllImport("USER32.dll", ExactSpelling = true)]
    private static extern DPI_AWARENESS_CONTEXT GetThreadDpiAwarenessContext();

    [DllImport("USER32.dll", ExactSpelling = true)]
    private static extern BOOL AreDpiAwarenessContextsEqual(DPI_AWARENESS_CONTEXT first, DPI_AWARENESS_CONTEXT second);

    [DllImport("USER32.dll", ExactSpelling = true)]
    private static extern int GetAwarenessFromDpiAwarenessContext(DPI_AWARENESS_CONTEXT value);

    [DllImport("USER32.dll", ExactSpelling = true)]
    private static extern BOOL IsProcessDPIAware();

    public static int DpiValid(int mode) => IsValidDpiAwarenessContext(new DPI_AWARENESS_CONTEXT(mode)).Value;

    public static int DpiAwareness(int mode) => GetAwarenessFromDpiAwarenessContext(new DPI_AWARENESS_CONTEXT(mode));

    public static int DpiApply(int mode)
    {
        // The generated WinForms imports read the OS error slot explicitly.
        Marshal.SetLastSystemError(0);
        int status = mode == 98 ? SetProcessDPIAware().Value :
            mode >= 99 ? SetProcessDpiAwareness(mode - 100).Value :
            SetProcessDpiAwarenessContext(new DPI_AWARENESS_CONTEXT(mode)).Value;
        Marshal.SetLastPInvokeError(Marshal.GetLastSystemError());
        return status;
    }

    public static int DpiCurrent()
    {
        // Windows returns opaque handles. Resolve their symbolic value by equality.
        DPI_AWARENESS_CONTEXT current = GetThreadDpiAwarenessContext();
        for (int mode = -5; mode <= -1; mode++)
            if (AreDpiAwarenessContextsEqual(current, new DPI_AWARENESS_CONTEXT(mode)).Value != 0)
                return mode;
        return 0;
    }

    public static int DpiIsAware() => IsProcessDPIAware().Value;
}
