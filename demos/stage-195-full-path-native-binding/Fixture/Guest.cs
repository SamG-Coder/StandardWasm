using System.Runtime.InteropServices;

public static unsafe class Guest
{
    public static int Query(nint path, int capacity, nint output, nint filePart) =>
        unchecked((int)GetFullPathNameW((char*)path, (uint)capacity, (char*)output, filePart));

    [DllImport("kernel32.dll", ExactSpelling = true)]
    private static extern uint GetFullPathNameW(char* path, uint capacity, char* output, nint filePart);
}
