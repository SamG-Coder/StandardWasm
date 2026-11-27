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

    private enum PROCESS_DPI_AWARENESS : int { Unaware, SystemAware, PerMonitorAware }

    [DllImport("api-ms-win-shcore-scaling-l1-1-1.dll", ExactSpelling = true)]
    private static unsafe extern HRESULT GetProcessDpiAwareness(HANDLE process, PROCESS_DPI_AWARENESS* awareness);

    private static unsafe HRESULT GetProcessDpiAwareness(HANDLE process, out PROCESS_DPI_AWARENESS awareness)
    {
        fixed (PROCESS_DPI_AWARENESS* pointer = &awareness)
            return GetProcessDpiAwareness(process, pointer);
    }

    public static unsafe int DpiReadProcessAwareness(int handle)
    {
        PROCESS_DPI_AWARENESS awareness = (PROCESS_DPI_AWARENESS)42;
        Marshal.SetLastSystemError(73);
        Marshal.SetLastPInvokeError(91);
        int result = GetProcessDpiAwareness(new HANDLE((void*)(nint)handle), out awareness).Value;
        int system = Marshal.GetLastSystemError();
        int pinvoke = Marshal.GetLastPInvokeError();
        int status = result == 0 ? 0 : result == unchecked((int)0x80070057) ? 1 : 2;
        return ((int)awareness + 1) * 1000000 + status * 100000 + system * 100 + pinvoke;
    }

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
