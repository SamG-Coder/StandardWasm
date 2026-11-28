using System.Runtime.InteropServices;
using Windows.Win32.Foundation;

public static unsafe class Guest
{
    public static nint ExecutableModule() => GetModuleHandleW(null);

    public static uint Register(nint address, bool captureError)
    {
        if (captureError) Marshal.SetLastSystemError(0);
        uint id = RegisterWindowMessageW(new PCWSTR((char*)address));
        if (captureError) Marshal.SetLastPInvokeError(Marshal.GetLastSystemError());
        return id;
    }

    public static void SetErrors(int system, int saved)
    {
        Marshal.SetLastPInvokeError(saved);
        Marshal.SetLastSystemError(system);
    }

    public static int Error() => Marshal.GetLastSystemError();
    public static int SavedError() => Marshal.GetLastPInvokeError();

    [DllImport("USER32.dll", ExactSpelling = true)]
    private static extern uint RegisterWindowMessageW(PCWSTR name);

    [DllImport("KERNEL32.dll", ExactSpelling = true)]
    private static extern nint GetModuleHandleW(char* name);
}

namespace Windows.Win32.Foundation
{
    [StructLayout(LayoutKind.Sequential)]
    internal readonly unsafe struct PCWSTR
    {
        private readonly char* value;
        internal PCWSTR(char* value) => this.value = value;
    }
}
