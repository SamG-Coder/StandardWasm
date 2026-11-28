using System.Runtime.InteropServices;
using Windows.Win32.Foundation;

public static unsafe class Guest
{
    public static nint Process() => GetCurrentProcess().Value;
    public static nint Thread() => GetCurrentThread().Value;
    public static uint ThreadId() => GetCurrentThreadId();
    public static nint Module() => GetModuleHandleW(null);
    public static uint WindowThread(nint handle) => GetWindowThreadProcessId(new HWND(handle), null);

    public static int Duplicate(nint process, nint handle, nint targetProcess, nint target) =>
        DuplicateHandle(new HANDLE(process), new HANDLE(handle), new HANDLE(targetProcess), (HANDLE*)target, 0, new BOOL(0), 2).Value;

    public static int ExitCode(nint handle, nint result) => GetExitCodeThread(new HANDLE(handle), (uint*)result).Value;

    public static uint ExitCodeOnStack(nint handle)
    {
        uint result = 0x0badcafe;
        _ = GetExitCodeThread(new HANDLE(handle), &result);
        return result;
    }

    // The oracle does not call declarations with deliberately incorrect pointees.
    public static int NarrowExit(nint handle, nint result) => GetExitCodeThreadNarrow(new HANDLE(handle), (ushort*)result).Value;

    public static int NarrowDuplicate(nint process, nint handle, nint result) =>
        DuplicateHandleNarrow(new HANDLE(process), new HANDLE(handle), new HANDLE(process), (ushort*)result, 0, new BOOL(0), 2).Value;

    public static int Close(nint handle) => CloseHandle(new HANDLE(handle)).Value;
    public static int SystemError() => Marshal.GetLastSystemError();
    public static int SavedError() => Marshal.GetLastPInvokeError();

    public static void SeedErrors(int system, int saved)
    {
        Marshal.SetLastPInvokeError(saved);
        Marshal.SetLastSystemError(system);
    }

    [DllImport("KERNEL32.dll", ExactSpelling = true)] private static extern HANDLE GetCurrentProcess();
    [DllImport("KERNEL32.dll", ExactSpelling = true)] private static extern HANDLE GetCurrentThread();
    [DllImport("KERNEL32.dll", ExactSpelling = true)] private static extern uint GetCurrentThreadId();
    [DllImport("KERNEL32.dll", ExactSpelling = true)] private static extern BOOL GetExitCodeThread(HANDLE handle, uint* code);

    [DllImport("KERNEL32.dll", ExactSpelling = true)]
    private static extern BOOL DuplicateHandle(HANDLE sourceProcess, HANDLE source, HANDLE targetProcess, HANDLE* target,
        uint access, BOOL inherit, uint options);

    [DllImport("KERNEL32.dll", ExactSpelling = true, EntryPoint = "GetExitCodeThread")]
    private static extern BOOL GetExitCodeThreadNarrow(HANDLE handle, ushort* code);

    [DllImport("KERNEL32.dll", ExactSpelling = true, EntryPoint = "DuplicateHandle")]
    private static extern BOOL DuplicateHandleNarrow(HANDLE sourceProcess, HANDLE source, HANDLE targetProcess, ushort* target,
        uint access, BOOL inherit, uint options);

    [DllImport("KERNEL32.dll", ExactSpelling = true)] private static extern BOOL CloseHandle(HANDLE handle);
    [DllImport("KERNEL32.dll", ExactSpelling = true)] private static extern nint GetModuleHandleW(char* name);
    [DllImport("USER32.dll", ExactSpelling = true)] private static extern uint GetWindowThreadProcessId(HWND handle, uint* process);
}

namespace Windows.Win32.Foundation
{
    internal readonly struct HANDLE
    {
        internal readonly nint Value;
        internal HANDLE(nint value) => Value = value;
    }

    internal readonly struct BOOL
    {
        internal readonly int Value;
        internal BOOL(int value) => Value = value;
    }

    internal readonly struct HWND
    {
        private readonly nint value;
        internal HWND(nint value) => this.value = value;
    }
}
