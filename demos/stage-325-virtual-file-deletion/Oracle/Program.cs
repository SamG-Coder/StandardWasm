if (!OperatingSystem.IsWindows() || IntPtr.Size != 4)
    throw new InvalidOperationException("The file deletion oracle requires Windows x86.");

string report = Path.GetFullPath(args[0]);
string originalDirectory = Directory.GetCurrentDirectory();
string wide = "wide λ🙂.txt";
string[] files = ["case.txt", "read only.txt", "Folder/inside.txt", wide, "space.txt"];
DirectoryInfo root = Directory.CreateTempSubdirectory("StandardWasm-delete-");
try
{
    Directory.SetCurrentDirectory(root.FullName);
    Directory.CreateDirectory("Folder");
    foreach (string file in files) File.WriteAllText(file, "fixture");
    File.SetAttributes("read only.txt", FileAttributes.ReadOnly);

    string[] paths = ["./Folder/../CASE.TXT", "case.txt", "missing.txt", "Missing/child.txt", "Folder",
        "read only.txt", "Folder/inside.txt", "Folder/inside.txt", wide, "space.txt. ", "", "read only.txt\\"];
    var cases = paths.Select(path => Observe(path)).ToArray();
    using var output = File.Create(report);
    System.Text.Json.JsonSerializer.Serialize(output, new { assembly = typeof(Guest).Assembly.Location,
        clr = Environment.Version.ToString(), cases });
}
finally
{
    Directory.SetCurrentDirectory(originalDirectory);
    string readOnly = Path.Combine(root.FullName, "read only.txt");
    if (File.Exists(readOnly)) File.SetAttributes(readOnly, FileAttributes.Normal);
    foreach (string file in files)
    {
        string path = Path.Combine(root.FullName, file);
        if (File.Exists(path)) File.Delete(path);
    }
    string folder = Path.Combine(root.FullName, "Folder");
    if (Directory.Exists(folder)) Directory.Delete(folder);
    root.Delete();
}

static unsafe object Observe(string path)
{
    fixed (char* pointer = path)
    {
        int result = Guest.Delete((nint)pointer);
        return new { path, result, error = Guest.SavedError() };
    }
}
