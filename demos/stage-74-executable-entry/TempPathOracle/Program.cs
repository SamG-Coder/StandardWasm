using System.Runtime.InteropServices;
using System.Text.Json;

if (args.Length != 1) throw new ArgumentException("Expected an oracle output path");
const string path = @"C:\StandardWasm\têmp-🛠\";
Environment.SetEnvironmentVariable("TMP", path);
var library = NativeLibrary.Load("kernel32.dll");
try
{
    var result = new
    {
        path = Path.GetTempPath(),
        clr = Environment.Version.ToString(),
        architecture = RuntimeInformation.ProcessArchitecture.ToString(),
        legacy = Observe(path, "GetTempPathW"),
        current = Observe(path, "GetTempPath2W"),
        registry = RegistryOracle.Observe(library),
        growth = ObserveGrowth()
    };
    File.WriteAllText(args[0], JsonSerializer.Serialize(result));
}
finally { NativeLibrary.Free(library); }

object ObserveGrowth()
{
    // CoreLib starts with 260 stack-allocated code units. A 260-unit path
    // needs one more unit for NUL, so this also exercises its grow-and-retry path.
    const string prefix = @"C:\StandardWasm\";
    string directory = prefix + new string('p', 260 - prefix.Length - 1) + '\\';
    Environment.SetEnvironmentVariable("TMP", directory);
    try
    {
        string normalized = Path.GetTempPath();
        if (!StringComparer.Ordinal.Equals(normalized, directory))
            throw new InvalidOperationException("CoreLib did not return the long directory");
        return new
        {
            path = normalized,
            legacy = Observe(directory, "GetTempPathW"),
            current = Observe(directory, "GetTempPath2W")
        };
    }
    finally { Environment.SetEnvironmentVariable("TMP", path); }
}

unsafe List<object> Observe(string directory, string entry)
{
    var query = (delegate* unmanaged[Stdcall]<uint, char*, uint>)NativeLibrary.GetExport(library, entry);
    int required = directory.Length + 1;
    var result = new List<object>();
    foreach (int capacity in new[] { 0, 1, required - 1, required, required + 1, 260 }.Distinct())
    {
        char[] destination = new string('!', capacity + 2).ToCharArray();
        Marshal.SetLastSystemError(777);
        Marshal.SetLastPInvokeError(321);
        fixed (char* buffer = destination)
        {
            uint count = query((uint)capacity, capacity == 0 ? null : buffer + 1);
            int system = Marshal.GetLastSystemError();
            int pinvoke = Marshal.GetLastPInvokeError();
            result.Add(new { capacity, count, system, pinvoke, text = new string(destination) });
        }
    }
    return result;
}
