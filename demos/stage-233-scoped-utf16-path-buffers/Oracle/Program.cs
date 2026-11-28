using System.Text.Json;

if (IntPtr.Size != 4) throw new InvalidOperationException("Use the Windows x86 CLR.");
string originalDirectory = Environment.CurrentDirectory;
string fixtureDirectory = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(args[0]))!, "windows-files");
string folder = Path.Combine(fixtureDirectory, "Long Folder \u03bb");
Directory.CreateDirectory(folder);
File.WriteAllText(Path.Combine(folder, "Long File \u03a9\U0001f642.txt"), "Path lookup fixture");
object results;
try
{
    Environment.CurrentDirectory = fixtureDirectory;
    results = new
    {
        Resolve = Guest.Resolve(), Exact = Guest.Exact(), Short = Guest.Short(), Query = Guest.Query(),
        MissingLeaf = Guest.MissingLeaf(), MissingDirectory = Guest.MissingDirectory(),
        InPlace = Guest.InPlace(), NativeInPlace = Guest.NativeInPlace(), RawQuery = Guest.RawQuery()
    };
}
finally
{
    Environment.CurrentDirectory = originalDirectory;
}
File.WriteAllText(args[0], JsonSerializer.Serialize(new { clr = Environment.Version.ToString(), results }));
