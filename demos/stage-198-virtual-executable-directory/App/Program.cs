using System.Runtime.InteropServices;

internal static unsafe class Program
{
    public static int Main()
    {
        const int capacity = 512;
        const string name = "payload.\u03bb";
        char* input = stackalloc char[32];
        char* working = stackalloc char[capacity];
        char* resolved = stackalloc char[capacity];
        nint* filePart = stackalloc nint[1];

        WritePath(".", input);
        uint workingLength = GetFullPathNameW(input, capacity, working, 0);
        if (workingLength < 3 || workingLength >= capacity) return 11;
        if (working[1] != ':' || working[2] != '\\') return 12;

        WritePath("assets\\..\\" + name, input);
        uint length = GetFullPathNameW(input, capacity, resolved, (nint)filePart);
        if (length != workingLength + 1 + name.Length || resolved[length] != 0) return 13;
        for (int i = 0; i < workingLength; i++)
            if (working[i] != resolved[i]) return 14;
        if (resolved[workingLength] != '\\' || *filePart != (nint)(resolved + workingLength + 1)) return 15;
        for (int i = 0; i < name.Length; i++)
            if (((char*)*filePart)[i] != name[i]) return 16;

        return 0x5000 + (int)workingLength;
    }

    private static void WritePath(string path, char* target)
    {
        for (int i = 0; i < path.Length; i++) target[i] = path[i];
        target[path.Length] = '\0';
    }

    [DllImport("kernel32.dll", ExactSpelling = true)]
    private static extern uint GetFullPathNameW(char* path, uint capacity, char* output, nint filePart);
}
