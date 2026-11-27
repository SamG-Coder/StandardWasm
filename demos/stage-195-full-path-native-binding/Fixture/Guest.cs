using System.Runtime.InteropServices;

public static unsafe class Guest
{
    public static int Query(nint path, int capacity, nint output, nint filePart) =>
        unchecked((int)GetFullPathNameW((char*)path, (uint)capacity, (char*)output, filePart));

    public static int ResolveRelative()
    {
        char* path = stackalloc char[5];
        path[0] = '.';
        path[1] = '.';
        path[2] = '\\';
        path[3] = '\u03bb';
        path[4] = '\0';
        char* output = stackalloc char[64];
        nint* filePart = stackalloc nint[1];
        uint length = GetFullPathNameW(path, 64, output, (nint)filePart);
        return length == 12 && output[0] == 'R' && output[2] == '\\' &&
            output[10] == '\\' && output[11] == '\u03bb' && output[12] == '\0' &&
            *filePart == (nint)(output + 11) ? 1 : 0;
    }

    [DllImport("kernel32.dll", ExactSpelling = true)]
    private static extern uint GetFullPathNameW(char* path, uint capacity, char* output, nint filePart);
}
