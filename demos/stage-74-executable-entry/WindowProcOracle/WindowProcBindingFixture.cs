using System.Runtime.InteropServices;
using Windows.Win32.Foundation;

public static unsafe class WindowProcBindingFixture
{
    public static nint Lookup(nint library, byte* name) =>
        GetProcAddress(new HMODULE((void*)library), new PCSTR(name)).Value;

    public static nint Dispatch(nint procedure, nint window, uint message, nuint wparam, nint lparam) =>
        CallWindowProcW((delegate* unmanaged[Stdcall]<HWND, uint, WPARAM, LPARAM, LRESULT>)procedure,
            new HWND((void*)window), message, new WPARAM(wparam), new LPARAM(lparam)).Value;

    [DllImport("KERNEL32.dll", ExactSpelling = true)]
    private static extern FARPROC GetProcAddress(HMODULE library, PCSTR name);

    [DllImport("USER32.dll", ExactSpelling = true)]
    private static extern LRESULT CallWindowProcW(
        delegate* unmanaged[Stdcall]<HWND, uint, WPARAM, LPARAM, LRESULT> procedure,
        HWND window, uint message, WPARAM wparam, LPARAM lparam);
}

namespace Windows.Win32.Foundation
{
    internal readonly unsafe struct HMODULE
    {
        internal readonly void* Value;
        internal HMODULE(void* value) => Value = value;
    }

    internal readonly unsafe struct PCSTR
    {
        internal readonly byte* Value;
        internal PCSTR(byte* value) => Value = value;
    }

    internal readonly struct FARPROC
    {
        internal readonly nint Value;
        internal FARPROC(nint value) => Value = value;
    }

    internal readonly unsafe struct HWND
    {
        internal readonly void* Value;
        internal HWND(void* value) => Value = value;
    }

    internal readonly struct WPARAM
    {
        internal readonly nuint Value;
        internal WPARAM(nuint value) => Value = value;
    }

    internal readonly struct LPARAM
    {
        internal readonly nint Value;
        internal LPARAM(nint value) => Value = value;
    }

    internal readonly struct LRESULT
    {
        internal readonly nint Value;
        internal LRESULT(nint value) => Value = value;
    }
}
