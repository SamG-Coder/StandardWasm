using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

[assembly: DisableRuntimeMarshalling]

public static class Guest
{
    public const string LongDirectory = "Long Project Folder";
    public const string LongFile = "Wide Name Example.document";
    public const string DirectoryAlias = "LONGPR~7";
    public const string FileAlias = "WIDENA~7.DOC";
    public const string NoAliasFile = "Long Without Alternate.example";
    public const string RemovedAlias = "LONGNO~7.EXA";
    public const int SystemSentinel = 17185, SavedSentinel = 25090;

    public static unsafe long Observe(int scenario)
    {
        string path = scenario switch
        {
            1 => "long project folder\\wide name example.document",
            2 or 3 or 4 or 5 => "longpr~7\\widena~7.doc",
            6 => "long project folder\\upper.txt",
            7 => "LONG WITHOUT ALTERNATE.EXAMPLE",
            8 => "LONGNO~7.EXA",
            9 => "Long Project Folder\\Missing Long Name.example",
            10 => "Missing Folder\\Missing Long Name.example",
            11 => "upper.txt\\child.txt",
            12 => "upper.txt\\",
            13 => "longpr~7\\",
            14 => "long project folder//upper.txt",
            15 => "lit~9.txt",
            16 => "Long Folder \u03bb\\Long File \u03a9\U0001f642.txt",
            17 => "./upper.txt",
            18 => "FrameworkOnly.dll",
            19 => "mountedonly.txt",
            20 => "upper.txt.",
            21 => "upper.txt",
            22 => "upper.txt",
            23 => "upper.txt",
            24 => @"c:\standardwasm\app\longpr~7\widena~7.doc",
            25 => @"\\?\c:\standardwasm\app\longpr~7\widena~7.doc",
            26 => "widena~7.doc",
            27 => @"C:\Different Mount\UPPER.TXT",
            28 => @"..\UPPER.TXT",
            _ => LongDirectory + "\\" + LongFile
        };
        char* source = stackalloc char[128];
        char* target = stackalloc char[128];
        for (int index = 0; index < 128; index++)
        {
            source[index] = '\u25a0';
            target[index] = '\u25a0';
        }
        int units = path.Length;
        for (int index = 0; index < units; index++) source[index] = path[index];
        source[units] = '\0';
        bool same = scenario == 3 || scenario == 4;
        char* output = same ? source : target;
        int capacity = scenario == 2 ? 0 : scenario == 4 || scenario == 5 ? 1 : scenario == 21 ? 10 : scenario == 23 ? 16 : 128;
        Marshal.SetLastPInvokeError(SavedSentinel);
        Marshal.SetLastSystemError(SystemSentinel);
        uint count = Raw(source,
            scenario == 2 || scenario == 22 ? null : scenario == 23 ? source + 1 : output,
            (uint)capacity);
        int error = Marshal.GetLastSystemError(), saved = Marshal.GetLastPInvokeError();

        // Capture all 128 destination units, including guard units beyond the offered capacity.
        int hash = 17;
        for (int index = 0; index < 128; index++) hash = unchecked(hash * 31 + output[index]);
        if (saved != SavedSentinel) return -1;
        return ((long)hash << 32) | ((long)error << 16) | count;
    }

    [DllImport("kernel32.dll", EntryPoint = "GetLongPathNameW", ExactSpelling = true)]
    private static extern unsafe uint Raw(char* source, char* output, uint capacity);
}
