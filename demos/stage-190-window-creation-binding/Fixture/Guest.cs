using System.Runtime.InteropServices;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

public static unsafe class Guest
{
    public static int Register(nint definition) => RegisterClassW((void*)definition);
    public static int SystemError() => Marshal.GetLastSystemError();

    public static nint Create(int exStyle, nint className, nint caption, int style,
        int x, int y, int width, int height, nint parent, nint menu, nint instance, nint data) =>
        CreateWindowExW((WINDOW_EX_STYLE)exStyle, new PCWSTR((char*)className),
            new PCWSTR((char*)caption), (WINDOW_STYLE)style, x, y, width, height,
            new HWND((void*)parent), new HMENU((void*)menu), new HINSTANCE(instance), (void*)data).Value;

    public static nint CreateDefaults() =>
        CreateWindowExW(0, default, default, 0, 0, 0, 0, 0, default, default, default, null).Value;

    [DllImport("USER32.dll", ExactSpelling = true)]
    private static extern HWND CreateWindowExW(WINDOW_EX_STYLE exStyle, PCWSTR className, PCWSTR caption,
        WINDOW_STYLE style, int x, int y, int width, int height, HWND parent, HMENU menu, HINSTANCE instance, void* data);

    [DllImport("USER32.dll", ExactSpelling = true)]
    private static extern ushort RegisterClassW(void* definition);
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
        private readonly void* pointer;
        internal HWND(void* value) => pointer = value;
        internal nint Value => (nint)pointer;
    }
}

namespace Windows.Win32.UI.WindowsAndMessaging
{
    internal enum WINDOW_EX_STYLE : uint { None }
    internal enum WINDOW_STYLE : uint { Overlapped }

    internal readonly unsafe struct HMENU
    {
        internal readonly void* Value;
        internal HMENU(void* value) => Value = value;
    }
}
