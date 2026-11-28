using System.Runtime.InteropServices;
using Windows.Win32.Foundation;

public static unsafe class Guest
{
    public static nint Query() => (nint)GetActiveWindow().Value;
    public static nint Activate(nint handle) => (nint)SetActiveWindow(new HWND((void*)handle)).Value;
    public static int SavedError() => Marshal.GetLastPInvokeError();
    public static int RejectWrongReturn() => WrongReturn();

    [DllImport("USER32.dll", ExactSpelling = true)]
    private static extern HWND GetActiveWindow();

    [DllImport("USER32.dll", ExactSpelling = true, SetLastError = true)]
    private static extern HWND SetActiveWindow(HWND window);

    [DllImport("USER32.dll", EntryPoint = "GetActiveWindow", ExactSpelling = true)]
    private static extern int WrongReturn();
}

namespace Windows.Win32.Foundation
{
    [StructLayout(LayoutKind.Sequential)]
    internal readonly unsafe struct HWND
    {
        internal readonly void* Value;
        internal HWND(void* value) => Value = value;
    }
}
