using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text.Json;

if (IntPtr.Size != 4)
    throw new InvalidOperationException("Expected Windows x86 CLR.");
string output = Path.GetFullPath(args[0]);
string root = Path.Combine(Path.GetDirectoryName(output)!, "windows-files-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
string originalDirectory = Environment.CurrentDirectory;
object[] entries =
[
    new { path = Guest.LongDirectory, kind = "directory", dosName = Guest.DirectoryAlias },
    new { path = Guest.LongDirectory + "/" + Guest.LongFile, kind = "file", dosName = Guest.FileAlias },
    new { path = Guest.LongDirectory + "/UPPER.TXT", kind = "file" },
    new { path = "UPPER.TXT", kind = "file" },
    new { path = Guest.NoAliasFile, kind = "file" },
    new { path = "LIT~9.TXT", kind = "file" },
    new { path = "Long Folder \u03bb", kind = "directory" },
    new { path = "Long Folder \u03bb/Long File \u03a9\U0001f642.txt", kind = "file" }
];
var results = new Dictionary<int, string>();
try
{
    Environment.CurrentDirectory = root;
    Directory.CreateDirectory(Guest.LongDirectory);
    string longPath = Path.Combine(Guest.LongDirectory, Guest.LongFile);
    File.WriteAllText(longPath, "fixture");
    File.WriteAllText(Guest.NoAliasFile, "fixture");
    File.WriteAllText(Path.Combine(Guest.LongDirectory, "UPPER.TXT"), "fixture");
    File.WriteAllText("UPPER.TXT", "fixture");
    File.WriteAllText("LIT~9.TXT", "fixture");
    Directory.CreateDirectory("Long Folder \u03bb");
    File.WriteAllText("Long Folder \u03bb\\Long File \u03a9\U0001f642.txt", "fixture");
    SetAlias(Guest.LongDirectory, Guest.DirectoryAlias);
    SetAlias(longPath, Guest.FileAlias);
    SetAlias(Guest.NoAliasFile, Guest.RemovedAlias);
    SetAlias(Guest.NoAliasFile, "");
    for (int scenario = 0; scenario <= 19; scenario++) results[scenario] = Guest.Observe(scenario).ToString();
    results[21] = Guest.Observe(21).ToString();
}
finally
{
    Environment.CurrentDirectory = originalDirectory;
}
File.WriteAllText(output, JsonSerializer.Serialize(new
{
    clr = Environment.Version + "/" + RuntimeInformation.ProcessArchitecture,
    fixture = typeof(Guest).Assembly.Location, entries, results
}));

static void SetAlias(string path, string alias)
{
    using var handle = CreateFile(path, 0x10000, 7, IntPtr.Zero, 3, 0x02000000, IntPtr.Zero);
    if (handle.IsInvalid || !SetFileShortName(handle, alias))
        throw new Win32Exception(Marshal.GetLastPInvokeError());
}

[DllImport("kernel32.dll", EntryPoint = "CreateFileW", ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = true)]
static extern Microsoft.Win32.SafeHandles.SafeFileHandle CreateFile(string name, uint access, uint sharing,
    IntPtr security, uint creation, uint flags, IntPtr template);
[DllImport("kernel32.dll", EntryPoint = "SetFileShortNameW", ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = true)]
[return: MarshalAs(UnmanagedType.Bool)]
static extern bool SetFileShortName(Microsoft.Win32.SafeHandles.SafeFileHandle handle, string alias);
