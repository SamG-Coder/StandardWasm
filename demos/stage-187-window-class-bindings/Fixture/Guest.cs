using System.Runtime.InteropServices;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

public static unsafe class Guest
{
    public static int Register(nint definition) => RegisterClassW((WNDCLASSW*)definition);

    public static int Lookup(nint module, nint name, nint output) =>
        GetClassInfoW(new HINSTANCE(module), new PCWSTR((char*)name), (WNDCLASSW*)output).Value;

    public static nint Replace(nint window, int index, nint procedure) =>
        SetWindowLongW(new HWND((void*)window), index, procedure);

    public static nint ReplacePointer(nint window, int index, nint procedure) =>
        SetWindowLongPtrW(new HWND((void*)window), index, procedure);

    public static nint Read(nint window, int index) => GetWindowLongW(new HWND((void*)window), index);

    public static int SavedError() => Marshal.GetLastPInvokeError();

    [DllImport("USER32.dll", ExactSpelling = true)]
    private static extern ushort RegisterClassW(WNDCLASSW* definition);

    [DllImport("USER32.dll", ExactSpelling = true)]
    private static extern BOOL GetClassInfoW(HINSTANCE module, PCWSTR name, WNDCLASSW* output);

    [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)]
    private static extern nint SetWindowLongW(HWND window, int index, nint procedure);

    [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)]
    private static extern nint SetWindowLongPtrW(HWND window, int index, nint procedure);

    [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)]
    private static extern nint GetWindowLongW(HWND window, int index);
}

namespace Windows.Win32.Foundation
{
    internal readonly struct HINSTANCE
    {
        internal readonly nint Value;
        internal HINSTANCE(nint value) => Value = value;
    }

    internal readonly unsafe struct PCWSTR
    {
        internal readonly char* Value;
        internal PCWSTR(char* value) => Value = value;
    }

    internal readonly unsafe struct HWND
    {
        internal readonly void* Value;
        internal HWND(void* value) => Value = value;
    }

    internal readonly struct BOOL
    {
        internal readonly int Value;
        internal BOOL(int value) => Value = value;
    }
}

namespace Windows.Win32.UI.WindowsAndMessaging
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct WNDCLASSW
    {
        internal uint Style;
        internal nint Procedure;
        internal int ClassExtra, WindowExtra;
        internal nint Module, Icon, Cursor, Background, Menu, Name;
    }
}
