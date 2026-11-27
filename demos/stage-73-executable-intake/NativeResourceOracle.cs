#:property PublishAot=false
#:property TargetFramework=net10.0-windows
#:property UseWindowsForms=true

using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;

if (args.Length != 2) throw new ArgumentException("Supply the apphost and single-file executable paths.");
var inputs = new[]
{
    (Path.GetFullPath(args[0]), 1),
    (Path.GetFullPath(args[1]), 1),
    (typeof(System.Windows.Forms.Application).Assembly.Location, 2)
};
Console.WriteLine(JsonSerializer.Serialize(inputs.Select(input => Read(input.Item1, input.Item2)).ToArray()));

static object Read(string path, int id)
{
    nint module = LoadLibraryExW(path, 0, 2); // LOAD_LIBRARY_AS_DATAFILE
    if (module == 0) throw new InvalidOperationException("Could not map the resource image.");
    try
    {
        nint resource = FindResourceExW(module, 24, id, 0); // RT_MANIFEST, neutral language
        if (resource == 0) throw new InvalidOperationException("Manifest resource was not found.");
        int size = SizeofResource(module, resource);
        nint buffer = LockResource(LoadResource(module, resource));
        if (size <= 0 || buffer == 0) throw new InvalidOperationException("Manifest bytes were not loaded.");
        byte[] bytes = new byte[size];
        Marshal.Copy(buffer, bytes, 0, size);
        return new { path, id, type = 24, language = 0, size, sha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant() };
    }
    finally
    {
        FreeLibrary(module);
    }
}

[DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
static extern nint LoadLibraryExW(string path, nint file, uint flags);
[DllImport("kernel32.dll", ExactSpelling = true)]
static extern nint FindResourceExW(nint module, nint type, nint id, ushort language);
[DllImport("kernel32.dll", ExactSpelling = true)]
static extern int SizeofResource(nint module, nint resource);
[DllImport("kernel32.dll", ExactSpelling = true)]
static extern nint LoadResource(nint module, nint resource);
[DllImport("kernel32.dll", ExactSpelling = true)]
static extern nint LockResource(nint resource);
[DllImport("kernel32.dll", ExactSpelling = true)]
[return: MarshalAs(UnmanagedType.Bool)]
static extern bool FreeLibrary(nint module);
