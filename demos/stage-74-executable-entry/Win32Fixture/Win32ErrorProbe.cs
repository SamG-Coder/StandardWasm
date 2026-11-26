using System.Runtime.InteropServices;

public static class Win32ErrorProbe
{
    public static int Exchange(int error)
    {
        int previous = Marshal.GetLastPInvokeError();
        Marshal.SetLastSystemError(error);
        Marshal.SetLastPInvokeError(Marshal.GetLastSystemError());
        return previous;
    }

    public static int Read() => Marshal.GetLastPInvokeError();
}
