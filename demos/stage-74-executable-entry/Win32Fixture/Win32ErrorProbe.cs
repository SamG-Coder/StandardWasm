using System.Runtime.InteropServices;
using Windows.Win32.Foundation;

public static class Win32ErrorProbe
{
    public static int Exchange(int error)
    {
        int previous = Marshal.GetLastPInvokeError();
        Marshal.SetLastSystemError(error);
        Marshal.SetLastPInvokeError(Marshal.GetLastSystemError());
        return previous;
    }

    public static unsafe int LookupModule(int scenario)
    {
        char* missing = stackalloc char[2];
        missing[0] = 'x';
        missing[1] = '\0';
        char* pointer = (scenario & 1) == 0 ? null : missing;

        Marshal.SetLastSystemError(0);
        bool found = (scenario & 2) == 0
            ? NativeModuleProbe.FromPointer(pointer) != 0
            : NativeModuleProbe.FromValue(new PCWSTR(pointer)).Value != null;
        Marshal.SetLastPInvokeError(Marshal.GetLastSystemError());
        return found ? 1 : Marshal.GetLastPInvokeError();
    }

    [DllImport("KERNEL32.dll", EntryPoint = "SetLastError", ExactSpelling = true, SetLastError = true)]
    private static extern void SetNativeError(int error);

    public static int MixedNativeErrors(int error)
    {
        Marshal.SetLastSystemError(19);
        Marshal.SetLastPInvokeError(73);
        SetNativeError(error);
        return Marshal.GetLastPInvokeError();
    }

    public static int Read() => Marshal.GetLastPInvokeError();

    public static int ReadSystemError() => Marshal.GetLastSystemError();
}
