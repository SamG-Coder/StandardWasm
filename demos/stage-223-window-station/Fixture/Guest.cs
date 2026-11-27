using System.Runtime.InteropServices;
using Windows.Win32.Foundation;
using Windows.Win32.System.StationsAndDesktops;

public static unsafe class Guest
{
    public static nint Station() => GetProcessWindowStation().Value;
    public static nint ScalarStation() => ScalarGetProcessWindowStation();
    public static nint Module() => GetModuleHandleW(null);
    public static int SystemError() => Marshal.GetLastSystemError();
    public static int SavedError() => Marshal.GetLastPInvokeError();

    public static void SeedErrors(int system, int saved)
    {
        Marshal.SetLastPInvokeError(saved);
        Marshal.SetLastSystemError(system);
    }

    public static int Flags(nint station, int index, nint address, uint size, nint length) =>
        GetUserObjectInformationW(new HANDLE(station), (USER_OBJECT_INFORMATION_INDEX)index, (void*)address, size, (uint*)length).Value;

    public static int ScalarFlags(nint station, int index, nint address, uint size, nint length) =>
        ScalarGetUserObjectInformationW(station, index, (void*)address, size, (uint*)length);

    // Compile this declaration as a negative control. The CLR oracle never calls it.
    public static int NarrowLength(nint station, nint address, nint length) =>
        NarrowGetUserObjectInformationW(new HANDLE(station), USER_OBJECT_INFORMATION_INDEX.Flags, (void*)address, 12, (ushort*)length).Value;

    [DllImport("KERNEL32.dll", ExactSpelling = true)]
    private static extern nint GetModuleHandleW(char* name);

    [DllImport("USER32.dll", ExactSpelling = true)]
    private static extern HWINSTA GetProcessWindowStation();

    [DllImport("USER32.dll", ExactSpelling = true)]
    private static extern BOOL GetUserObjectInformationW(HANDLE station, USER_OBJECT_INFORMATION_INDEX index, void* address,
        uint size, uint* length);

    [DllImport("user32.dll", ExactSpelling = true, EntryPoint = "GetProcessWindowStation")]
    private static extern nint ScalarGetProcessWindowStation();

    [DllImport("user32.dll", ExactSpelling = true, EntryPoint = "GetUserObjectInformationW")]
    private static extern int ScalarGetUserObjectInformationW(nint station, int index, void* address, uint size, uint* length);

    [DllImport("USER32.dll", ExactSpelling = true, EntryPoint = "GetUserObjectInformationW")]
    private static extern BOOL NarrowGetUserObjectInformationW(HANDLE station, USER_OBJECT_INFORMATION_INDEX index, void* address,
        uint size, ushort* length);
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
}

namespace Windows.Win32.System.StationsAndDesktops
{
    internal readonly struct HWINSTA
    {
        internal readonly nint Value;
        internal HWINSTA(nint value) => Value = value;
    }

    internal enum USER_OBJECT_INFORMATION_INDEX { Flags = 1 }
}
