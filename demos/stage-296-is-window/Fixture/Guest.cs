using System.Runtime.InteropServices;
using Windows.Win32.Foundation;

public static unsafe class Guest
{
    public static int Query(nint handle) => IsWindow(new HWND((void*)handle)).Value != 0 ? 1 : 0;

    public static int RejectWrongWord(int handle) => WrongWord(handle).Value;
    public static int RejectWrongWrapper(nint handle) => WrongWrapper(new OtherHandle((void*)handle)).Value;

    [DllImport("USER32.dll", ExactSpelling = true)]
    private static extern BOOL IsWindow(HWND window);

    [DllImport("USER32.dll", EntryPoint = "IsWindow", ExactSpelling = true)]
    private static extern BOOL WrongWord(int window);

    [DllImport("USER32.dll", EntryPoint = "IsWindow", ExactSpelling = true)]
    private static extern BOOL WrongWrapper(OtherHandle window);

    private readonly struct OtherHandle
    {
        private readonly void* value;
        internal OtherHandle(void* address) => value = address;
    }
}

namespace Windows.Win32.Foundation
{
    [StructLayout(LayoutKind.Sequential)]
    internal readonly unsafe struct HWND
    {
        private readonly void* value;
        internal HWND(void* address) => value = address;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal readonly struct BOOL
    {
        internal readonly int Value;
    }
}
