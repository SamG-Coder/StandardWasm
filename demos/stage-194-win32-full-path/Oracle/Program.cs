using System.Runtime.InteropServices;
using System.Text.Json;

if (IntPtr.Size != 4) throw new InvalidOperationException("The path oracle requires Windows x86.");
string originalDirectory = Environment.CurrentDirectory;
string directory = Path.GetFullPath(args[1]);
Directory.CreateDirectory(directory);
string driveRoot = Path.GetPathRoot(directory)!;
string drive = driveRoot[..2];
string otherDrive = drive.StartsWith("Q", StringComparison.OrdinalIgnoreCase) ? "X:" : "Q:";
try
{
    Environment.CurrentDirectory = directory;
    string collapsing = @".\folder\..\É漢.txt";
    int upperBound = (int)Oracle.Query(collapsing, 0, true).Length;
    int normalizedLength = (int)Oracle.Query(collapsing, 512, true).Length;
    string longPath = driveRoot + new string('a', 240) + @"\" + new string('b', 30) + ".txt";
    var cases = new List<Oracle.Case>
    {
        new("absolute", drive + @"\folder\..\ROOT.TXT"),
        new("absolute-lowercase-drive", drive.ToLowerInvariant() + @"\Folder\..\U.txt"),
        new("absolute-root", driveRoot),
        new("relative", collapsing),
        new("relative-forward-slashes", "foo/a//../É漢.txt"),
        new("relative-root-floor", string.Concat(Enumerable.Repeat(@"..\", 40)) + "file"),
        new("current-directory", "."),
        new("current-directory-with-slash", @".\"),
        new("parent-directory", ".."),
        new("parent-directory-with-slash", @"..\"),
        new("rooted", @"\folder\..\file"),
        new("rooted-forward", "/folder/../file"),
        new("current-drive-relative", drive.ToLowerInvariant() + @"relative\file"),
        new("current-drive-only", drive),
        new("other-drive-relative", otherDrive.ToLowerInvariant() + @"a\..\file"),
        new("other-drive-only", otherDrive),
        new("unc", @"\\virtual.invalid\share\folder\..\file"),
        new("unc-share-floor", @"//virtual.invalid/share/../../leaf"),
        new("unc-share-root", @"\\virtual.invalid\share"),
        new("unc-share-trailing-slash", @"\\virtual.invalid\share\"),
        new("directory-with-slash", drive + @"\folder\"),
        new("existing-directory-is-lexical", directory),
        new("terminal-dots-and-spaces", drive + @"\folder\file..  "),
        new("terminal-component-trims-away", drive + @"\folder\.. "),
        new("supplementary-character", drive + @"\foo\😀.txt"),
        new("short-name-text-is-lexical", drive + @"\folder\PROGRA~1\word.txt"),
        new("capacity-zero", collapsing, 0),
        new("capacity-one", collapsing, 1),
        new("normalized-length-is-still-short", collapsing, normalizedLength + 1),
        new("one-short-of-required", collapsing, upperBound - 1),
        new("exact-retry-capacity", collapsing, upperBound),
        new("no-file-part", collapsing, upperBound + 9, false),
        new("other-drive-preflight", otherDrive + "short", 3),
        new("other-drive-prefix-fits", otherDrive + "short", 4),
        new("other-drive-root-prefix-fits", otherDrive, 4),
        new("empty", ""),
        new("all-spaces", "  "),
        new("over-max-path-size", longPath, 260),
        new("over-max-path-success", longPath),
        new("unicode-string-max", driveRoot + new string('a', 32763), 0),
        new("unicode-string-overflow", driveRoot + new string('a', 32764), 0)
    };
    object nested = Oracle.Evaluate(cases);
    Environment.CurrentDirectory = driveRoot;
    object root = Oracle.Evaluate([
        new("drive-root-dot-query", ".", 0), new("drive-root-dot-small", ".", 2),
        new("drive-root-dot-three", ".", 3), new("drive-root-dot-four", ".", 4),
        new("drive-root-dot-full", ".", 5), new("drive-root-relative", "file"),
        new("drive-root-current-drive-only", drive)
    ]);
    File.WriteAllText(args[0], JsonSerializer.Serialize(new { clr = Environment.Version.ToString(), scenarios = new[] { nested, root } }));
}
finally { Environment.CurrentDirectory = originalDirectory; }

internal static unsafe class Oracle
{
    private const int ErrorSentinel = 0x4321, PartSentinel = 0x12345678;
    private const char BufferSentinel = '◇';
    [DllImport("kernel32.dll", ExactSpelling = true)]
    private static extern uint GetFullPathNameW(char* fileName, uint size, char* output, char** filePart);

    internal sealed record Case(string Key, string Path, int Capacity = 512, bool HasFilePart = true);
    internal sealed record Result(uint Length, int Error, string Text, bool Terminator, string FilePart, int PartOffset,
        bool CapacityBoundary, bool TailClear);

    internal static object Evaluate(IEnumerable<Case> cases) => new
    {
        currentDirectory = Environment.CurrentDirectory,
        cases = cases.Select(item => new { key = item.Key, path = item.Path, capacity = item.Capacity,
            hasFilePart = item.HasFilePart, outcome = Query(item.Path, item.Capacity, item.HasFilePart) }).ToArray()
    };

    internal static Result Query(string path, int capacity, bool hasFilePart)
    {
        char[] output = Enumerable.Repeat(BufferSentinel, capacity + 2).ToArray();
        char* part = (char*)PartSentinel;
        uint length;
        int error;
        int offset;
        string partKind;
        fixed (char* source = path)
        fixed (char* target = output)
        {
            Marshal.SetLastSystemError(ErrorSentinel);
            length = GetFullPathNameW(source, (uint)capacity, capacity == 0 ? null : target, hasFilePart ? &part : null);
            error = Marshal.GetLastSystemError();
            partKind = part == null ? "null" : part == (char*)PartSentinel ? "unchanged" :
                part >= target && part < target + capacity ? "output" : "unexpected";
            offset = partKind == "output" ? checked((int)(part - target)) : -1;
        }
        bool success = length > 0 && length < capacity;
        bool terminator = success ? output[length] == '\0' : capacity > 0 && output[0] == '\0';
        string text = success ? new string(output, 0, (int)length) : null;
        // Native short-buffer routines can leave an intermediate path prefix
        // behind the leading NUL. Only successful output has a defined string.
        bool tailClear = !success || output.Skip((int)length).Take(capacity - (int)length).All(c => c == '\0');
        return new(length, error, text, terminator, partKind, offset,
            output[capacity] == BufferSentinel && output[capacity + 1] == BufferSentinel, tailClear);
    }
}
