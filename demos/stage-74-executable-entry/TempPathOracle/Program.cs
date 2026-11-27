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
        legacy = Observe("GetTempPathW"),
        current = Observe("GetTempPath2W"),
        registry = RegistryOracle.Observe(library)
    };
    File.WriteAllText(args[0], JsonSerializer.Serialize(result));
}
finally { NativeLibrary.Free(library); }

unsafe List<object> Observe(string entry)
{
    var query = (delegate* unmanaged[Stdcall]<uint, char*, uint>)NativeLibrary.GetExport(library, entry);
    int required = path.Length + 1;
    var result = new List<object>();
    foreach (int capacity in new[] { 0, 1, required - 1, required, required + 1, 260 })
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
