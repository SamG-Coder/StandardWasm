using System.Runtime.InteropServices;
using Windows.Win32.Foundation;

public static unsafe class Guest
{
    public static uint Query(nint window, nint process) =>
        GetWindowThreadProcessId(new HWND((void*)window), (uint*)process);

    public static nint Module() => GetModuleHandleW(null);

    public static uint WrongPointee(nint window, nint process) =>
        GetWindowThreadProcessIdWithCharPointer(new HWND((void*)window), (char*)process);

    public static void SeedErrors(int systemError, int savedError)
    {
        Marshal.SetLastSystemError(systemError);
        Marshal.SetLastPInvokeError(savedError);
    }

    public static int SystemError() => Marshal.GetLastSystemError();
    public static int SavedError() => Marshal.GetLastPInvokeError();

    [DllImport("USER32.dll", ExactSpelling = true)]
    private static extern uint GetWindowThreadProcessId(HWND window, uint* process);

    [DllImport("USER32.dll", ExactSpelling = true, EntryPoint = "GetWindowThreadProcessId")]
    private static extern uint GetWindowThreadProcessIdWithCharPointer(HWND window, char* process);

    [DllImport("KERNEL32.dll", ExactSpelling = true)]
    private static extern nint GetModuleHandleW(char* name);
}

namespace Windows.Win32.Foundation
{
    internal readonly unsafe struct HWND
    {
        private readonly void* pointer;
        internal HWND(void* value) => pointer = value;
    }
}
