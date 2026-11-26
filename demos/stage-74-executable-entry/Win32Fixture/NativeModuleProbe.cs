using System.Runtime.InteropServices;

namespace Windows.Win32.Foundation
{
    internal unsafe struct PCWSTR
    {
        internal readonly char* Value;
        internal PCWSTR(char* pointer) => Value = pointer;
    }

    internal unsafe struct HMODULE
    {
        internal readonly void* Value;
        internal HMODULE(void* value) => Value = value;
    }
}

internal static unsafe class NativeModuleProbe
{
    [DllImport("KERNEL32.dll", EntryPoint = "GetModuleHandleW", ExactSpelling = true)]
    internal static extern Windows.Win32.Foundation.HMODULE FromValue(Windows.Win32.Foundation.PCWSTR name);

    [DllImport("KERNEL32.dll", EntryPoint = "GetModuleHandleW", ExactSpelling = true)]
    internal static extern nint FromPointer(char* name);
}
