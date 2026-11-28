using System.Runtime.InteropServices;

public static class Guest
{
    public static nint Focus() => GetFocus();
    public static nint Active() => GetActiveWindow();
    public static nint Activate(nint handle) => SetActiveWindow(handle);
    public static nint Pick(nint handle) => SetFocus(handle);
    public static int Enable(nint handle, int enable) => EnableWindow(handle, enable);
    public static int SavedError() => Marshal.GetLastPInvokeError();
    public static int RejectWrongReturn() => WrongReturn();

    [DllImport("USER32.dll", ExactSpelling = true)]
    private static extern nint GetFocus();
    [DllImport("USER32.dll", ExactSpelling = true)]
    private static extern nint GetActiveWindow();
    [DllImport("USER32.dll", ExactSpelling = true, SetLastError = true)]
    private static extern nint SetActiveWindow(nint handle);
    [DllImport("USER32.dll", ExactSpelling = true, SetLastError = true)]
    private static extern nint SetFocus(nint handle);
    [DllImport("USER32.dll", ExactSpelling = true)]
    private static extern int EnableWindow(nint handle, int enable);
    [DllImport("USER32.dll", EntryPoint = "GetFocus", ExactSpelling = true)]
    private static extern int WrongReturn();
}
