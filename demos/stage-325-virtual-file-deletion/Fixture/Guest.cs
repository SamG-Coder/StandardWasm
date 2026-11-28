using System.Runtime.InteropServices;

public static unsafe class Guest
{
    public static int Delete(nint path) => DeleteFileW((ushort*)path);
    public static uint Lookup(nint path, nint output, uint capacity) => GetLongPathNameW((char*)path, (char*)output, capacity);
    public static int SavedError() => Marshal.GetLastPInvokeError();
    public static nint Module() => GetModuleHandleW(null);
    public static int Wrong(nint path) => WrongPointee((byte*)path);

    [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
    private static extern int DeleteFileW(ushort* path);
    [DllImport("kernel32.dll", ExactSpelling = true)]
    private static extern uint GetLongPathNameW(char* path, char* output, uint length);
    [DllImport("KERNEL32.dll", ExactSpelling = true)]
    private static extern nint GetModuleHandleW(char* path);
    [DllImport("kernel32.dll", ExactSpelling = true, EntryPoint = "DeleteFileW", SetLastError = true)]
    private static extern int WrongPointee(byte* path);
}
