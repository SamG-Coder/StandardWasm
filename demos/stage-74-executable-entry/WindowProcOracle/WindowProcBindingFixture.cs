using System.Runtime.InteropServices;
using Windows.Win32.Foundation;

public static unsafe class WindowProcBindingFixture
{
    public static nint Lookup(nint library, byte* name) =>
        GetProcAddress(new HMODULE((void*)library), new PCSTR(name)).Value;

    [DllImport("KERNEL32.dll", ExactSpelling = true)]
    private static extern FARPROC GetProcAddress(HMODULE library, PCSTR name);
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
}
