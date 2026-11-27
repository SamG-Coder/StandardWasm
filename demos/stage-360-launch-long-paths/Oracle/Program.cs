using System.Runtime.InteropServices;
using System.Text.Json;

// args: <output.json> <launch directory holding the published files> <empty scratch directory>
if (IntPtr.Size != 4)
    throw new InvalidOperationException("Expected Windows x86 CLR.");
string output = Path.GetFullPath(args[0]);
string launch = Path.GetFullPath(args[1]);
string matrix = Path.GetFullPath(args[2]);
string original = Environment.CurrentDirectory;
var results = new Dictionary<int, string>();
try
{
    // The fixture resolves its inputs against the working directory, as an app started from its folder.
    Environment.CurrentDirectory = launch;
    for (int scenario = 0; scenario <= LaunchLongPaths.LastScenario; scenario++)
        results[scenario] = LaunchLongPaths.Observe(scenario).ToString();
}
finally
{
    Environment.CurrentDirectory = original;
}

// Oracle case 14: the real folder's ancestors spelled by their DOS aliases.
string shortDirectory = ShortPath(launch);
string aliasInput = shortDirectory + "\\Data Ω\\Wide Ω\U0001f642.txt";
var alias = LongPath(aliasInput, 1024);

// Which components GetLongPathNameW respells, and what a short buffer receives.
// Every name is queried with its letter case swapped, so a respelled component
// is visible. The browser replays these rows on a mount with the same entries.
string[] directories = { "Data Ω", "Long Directory Name", "Data Ω/Long Inner Folder", "Data Ω/Long Inner Folder/Next" };
string[] files =
{
    "abcdefgh.txt", "abcdefghi.tx", "ab.cdef", "a.b.c", "x+y.txt", "x,y.txt", "x[y.txt", "x;y.txt", "x=y.txt",
    "Äbc.txt", "Ωab.txt", "\U0001f642.txt", "a b", "Abcdefghi", "Abcdefgh", "Abcdefghij", "Ab.Cd",
    "AbcdefghijK.t", ".Hidden", "A~1x.txt", "Abßx.txt", "Abā.txt", "Ab中.txt",
    "Data Ω/Long File Name.txt", "Data Ω/Ab.txt", "Long Directory Name/Ab.txt", "Long Directory Name/Long File Name.txt",
    "Data Ω/Long Inner Folder/Ab.txt", "Data Ω/Long Inner Folder/Next/Long Leaf Name.txt"
};
Directory.CreateDirectory(matrix);
if (Directory.EnumerateFileSystemEntries(matrix).Any()) throw new InvalidOperationException("Expected an empty matrix directory.");
foreach (var directory in directories) Directory.CreateDirectory(Path.Combine(matrix, directory.Replace('/', '\\')));
foreach (var file in files) File.WriteAllText(Path.Combine(matrix, file.Replace('/', '\\')), "fixture");
var queries = files.Select(file => (Swap(file.Replace('/', '\\')), 300u)).ToList();
queries.AddRange(new[]
{
    ("long directory name\\aB.TXT", 25u), ("long directory name\\aB.TXT", 21u), ("long directory name\\aB.TXT", 20u),
    ("data ω\\aB.TXT", 8u), ("data ω\\aB.TXT", 7u), ("data ω\\aB.TXT", 6u), ("data ω\\aB.TXT", 1u),
    ("long directory name\\long file name.txt", 30u), ("Long Directory Name\\Ab.txt", 22u),
    (matrix + "\\Data Ω\\Long File Name.txt", (uint)matrix.Length + 4), (matrix + "\\Data Ω\\Long File Name.txt", (uint)matrix.Length + 9),
    (matrix + "\\Long Directory Name\\Long File Name.txt", (uint)matrix.Length + 30),
    ("abcdefgh.txt", 12u), ("abcdefgh.txt", 1u), ("Abcdefghij", 10u)
});
// The run of separators and long components before a short one is written as one piece.
foreach (string pending in new[] { @"data ω\long inner folder\aB.TXT", @"data ω\\long inner folder\\aB.TXT",
    "data ω/long inner folder/aB.TXT", @"data ω\long inner folder\nEXT\long leaf name.txt" })
    foreach (uint capacity in new uint[] { 7, 8, 24, 25, 26, 27, 29, 30 }) queries.Add((pending, capacity));
var rows = new List<object>();
try
{
    Environment.CurrentDirectory = matrix;
    foreach (var (path, capacity) in queries) rows.Add(new { path, capacity, result = LongPath(path, capacity) });
}
finally
{
    Environment.CurrentDirectory = original;
}

// Windows' case-insensitive name table, compared with the browser's copy.
var upcase = new int[65536];
for (int unit = 0; unit < upcase.Length; unit++) upcase[unit] = RtlUpcaseUnicodeChar((ushort)unit);

File.WriteAllText(output, JsonSerializer.Serialize(new
{
    clr = Environment.Version + "/" + RuntimeInformation.ProcessArchitecture,
    launch, shortDirectory, results, aliasInput, alias,
    matrix = new { root = matrix, directories, files, rows },
    upcase
}));

static string Swap(string text) => new(text.Select(unit =>
    char.IsUpper(unit) ? char.ToLowerInvariant(unit) : char.IsLower(unit) ? char.ToUpperInvariant(unit) : unit).ToArray());

// The raw result, the error on failure, and every buffer unit the call changed
// (the buffer starts filled with '#'), up to the first unchanged unit.
static unsafe object LongPath(string path, uint capacity)
{
    char* buffer = stackalloc char[1026];
    for (int index = 0; index < 1026; index++) buffer[index] = '#';
    uint length = GetLongPathNameW(path, buffer, capacity);
    int error = length == 0 ? Marshal.GetLastPInvokeError() : 0;
    int changed = 0;
    while (changed < 1026 && buffer[changed] != '#') changed++;
    for (int index = changed; index < 1026; index++)
        if (buffer[index] != '#') throw new InvalidOperationException("Non-contiguous write for " + path);
    return new { length, error, written = new string(buffer, 0, changed) };
}

static unsafe string ShortPath(string path)
{
    char* buffer = stackalloc char[1024];
    uint length = GetShortPathNameW(path, buffer, 1024);
    if (length == 0 || length >= 1024) throw new System.ComponentModel.Win32Exception(Marshal.GetLastPInvokeError());
    return new string(buffer, 0, (int)length);
}

[DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
static extern unsafe uint GetShortPathNameW(string path, char* output, uint capacity);
[DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
static extern unsafe uint GetLongPathNameW(string path, char* output, uint capacity);
[DllImport("ntdll.dll", ExactSpelling = true)]
static extern ushort RtlUpcaseUnicodeChar(ushort unit);
