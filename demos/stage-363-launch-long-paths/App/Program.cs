using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

[assembly: DisableRuntimeMarshalling]

// Issue #275: GetLongPathNameW against the directory that holds the launched
// executable. Absolute inputs start with GetFullPathNameW("."), and every result
// is reported relative to that machine-specific prefix, so Windows (a real publish
// folder) and the browser (the virtual C:\StandardWasm\App mount) agree exactly.
// Inputs are assembled in UTF-16 buffers from literals only.
public static unsafe class LaunchLongPaths
{
    public const int SystemSentinel = 17185, SavedSentinel = 25090, LastScenario = 23;
    // Each row offers prefix + Window units and hashes the Window + 2 units after
    // the prefix, so the hashed span does not depend on the launch folder's length.
    const int Buffer = 640, Window = 256;
    const string Relative = "Data \u03a9\\Wide \u03a9\U0001f642.txt";

    public static int Main()
    {
        // Fold every scenario into the exit status so the unchanged launch path can compare it.
        int hash = 23;
        for (int scenario = 0; scenario <= LastScenario; scenario++)
        {
            long result = Observe(scenario);
            hash = unchecked(hash * 31 + (int)result);
            hash = unchecked(hash * 31 + (int)(result >> 32));
        }
        return hash & 0x7fffffff;
    }

    // Returns (output hash << 32) | (raw error << 16) | (count - prefix), or a
    // value from -1 to -4 when the fixture itself detects a broken invariant.
    public static long Observe(int scenario)
    {
        char* dot = stackalloc char[2];
        char* home = stackalloc char[Buffer];
        Append(dot, 0, ".");
        int length = (int)GetFullPathNameW(dot, Buffer, home, 0);
        if (length < 3 || length >= Buffer - Window - 80) return -2;
        int parent = length - 1;
        while (home[parent] != '\\') parent--;
        int up = parent == 2 ? 3 : parent;

        char* source = stackalloc char[Buffer];
        Fill(source);
        int units, prefix = 0;
        switch (scenario)
        {
            case 5: units = Append(source, 0, "data \u03c9\\WIDE \u03c9\U0001f642.TXT"); break;
            case 6: case 7:
                prefix = Copy(source, 0, home, length);
                if (scenario == 7) Lower(source, prefix);
                units = Append(source, Append(source, prefix, "\\"), Relative);
                break;
            case 8:
                prefix = Copy(source, Append(source, 0, "\\\\?\\"), home, length);
                units = Append(source, Append(source, prefix, "\\"), Relative);
                break;
            case 9: units = 0; break;
            case 10: units = Append(source, 0, "Data \u03a9\\Missing.txt"); break;
            case 11: units = Append(source, 0, "Missing \u03a9\\Missing.txt"); break;
            case 12: units = Append(source, 0, "LaunchLongPaths.dll\\"); break;
            case 15: units = Append(source, 0, "Data \u03a9\\"); break;
            case 16: units = Append(source, 0, "LaunchLongPaths.dll"); break;
            case 17: units = Append(source, 0, "launchlongpaths.runtimeconfig.json"); break;
            case 18: prefix = Copy(source, 0, home, up); units = Append(source, prefix, "\\Not Present \u03a9.txt"); break;
            case 19: prefix = Copy(source, 0, home, up); units = Append(source, prefix, "\\Absent Folder \u03a9\\Missing.txt"); break;
            case 20: units = prefix = Copy(source, 0, home, up); break;
            case 21: units = prefix = Copy(source, 0, home, 3); break;
            case 22: units = Append(source, 0, "Data \u03a9/Wide \u03a9\U0001f642.txt"); break;
            case 23: units = Append(source, Append(source, 0, ".\\"), Relative); break;
            default: units = Append(source, 0, Relative); break;
        }
        source[units] = '\0';
        int capacity = scenario == 1 ? units + 1 : scenario == 2 ? units : scenario == 3 ? 0 : scenario == 14 ? 2 : prefix + Window;
        return Query(source, prefix, capacity, scenario == 3, scenario == 13 || scenario == 14);
    }

    static long Query(char* source, int prefix, int capacity, bool nullTarget, bool inPlace)
    {
        char* target = stackalloc char[Buffer];
        Fill(target);
        char* output = inPlace ? source : target;
        Marshal.SetLastPInvokeError(SavedSentinel);
        Marshal.SetLastSystemError(SystemSentinel);
        uint count = GetLongPathNameW(source, nullTarget ? null : output, (uint)capacity);
        int error = Marshal.GetLastSystemError(), saved = Marshal.GetLastPInvokeError();
        if (saved != SavedSentinel) return -1;

        // Hash the whole buffer, guards included, except the machine-specific prefix.
        // That prefix must instead repeat the caller's spelling; Windows returns
        // stored casing for DOS-shaped components such as "App", so it compares
        // ASCII case-insensitively. In-place rows have no prefix.
        int hash = 17;
        bool succeeded = count != 0 && count < capacity;
        for (int index = 0; index < prefix + Window + 2; index++)
        {
            if (index < prefix)
            {
                if (succeeded && Upper(output[index]) != Upper(source[index])) return -4;
                continue;
            }
            hash = unchecked(hash * 31 + output[index]);
        }
        int adjusted = count == 0 ? 0 : (int)count - prefix;
        return ((long)hash << 32) | ((long)error << 16) | (uint)(adjusted & 0xffff);
    }

    static char Upper(char unit) => unit >= 'a' && unit <= 'z' ? (char)(unit - 32) : unit;

    static void Lower(char* text, int units)
    {
        for (int index = 0; index < units; index++)
            if (text[index] >= 'A' && text[index] <= 'Z') text[index] = (char)(text[index] + 32);
    }

    static void Fill(char* buffer)
    {
        for (int index = 0; index < Buffer; index++) buffer[index] = '#';
    }

    static int Append(char* target, int at, string text)
    {
        for (int index = 0; index < text.Length; index++) target[at + index] = text[index];
        target[at + text.Length] = '\0';
        return at + text.Length;
    }

    static int Copy(char* target, int at, char* source, int units)
    {
        for (int index = 0; index < units; index++) target[at + index] = source[index];
        return at + units;
    }

    [DllImport("kernel32.dll", ExactSpelling = true)]
    static extern uint GetFullPathNameW(char* path, uint capacity, char* output, nint filePart);

    [DllImport("kernel32.dll", ExactSpelling = true)]
    static extern uint GetLongPathNameW(char* path, char* output, uint capacity);
}
