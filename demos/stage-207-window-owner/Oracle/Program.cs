using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;

if (IntPtr.Size != 4) throw new InvalidOperationException("Expected the Windows x86 CLR.");
uint thread = NativeMethods.GetCurrentThreadId();
uint process = (uint)Environment.ProcessId;
var first = CreateWindow();
var second = CreateWindow();
try
{
    var withOutput = Owner(first.Handle);
    var noOutput = Thread(second.Handle);
    var otherWindow = Owner(second.Handle);
    nint stale = first.Handle;
    first.DestroyHandle();
    var results = new
    {
        withOutput, noOutput, otherWindow, invalid = Owner(0), stale = Owner(stale),
        invalidNoOutput = Thread(0)
    };
    Assembly core = Assembly.Load("System.Private.Windows.Core");
    var leaf = (MethodInfo)core.ManifestModule.ResolveMethod(0x060000e8)!;
    var import = leaf.GetCustomAttribute<DllImportAttribute>()!;
    File.WriteAllText(args[0], JsonSerializer.Serialize(new
    {
        architecture = RuntimeInformation.ProcessArchitecture.ToString(), clr = Environment.Version.ToString(),
        fixture = typeof(Guest).Assembly.Location, results,
        original = new { row = leaf.MetadataToken & 0xffffff, library = import.Value, entry = import.EntryPoint,
            import.SetLastError, signature = Convert.ToHexString(leaf.Module.ResolveSignature(leaf.MetadataToken)),
            sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(core.Location))) }
    }));
}
finally
{
    if (first.Handle != 0) first.DestroyHandle();
    second.DestroyHandle();
}

static NativeWindow CreateWindow()
{
    var window = new NativeWindow();
    window.CreateHandle(new CreateParams { Caption = "StandardWasm owner oracle", Style = unchecked((int)0x80000000),
        X = 13, Y = 17, Width = 161, Height = 103 });
    if (window.Handle == 0) throw new InvalidOperationException("Expected a real Windows HWND.");
    return window;
}

unsafe object Owner(nint window)
{
    const uint sentinel = 0x89abcdef, guard = 0x76543210;
    uint* buffer = stackalloc uint[3] { guard, sentinel, guard };
    Guest.SeedErrors(17185, 25106);
    uint actualThread = Guest.Query(window, (nint)(buffer + 1));
    int system = Guest.SystemError(), saved = Guest.SavedError();
    uint actualProcess = buffer[1];
    return new { thread = actualThread == thread, zeroThread = actualThread == 0, process = actualProcess == process,
        unchangedOutput = actualProcess == sentinel, system, saved, guards = buffer[0] == guard && buffer[2] == guard };
}

object Thread(nint window)
{
    Guest.SeedErrors(17185, 25106);
    uint result = Guest.Query(window, 0);
    return new { thread = result == thread, zeroThread = result == 0, system = Guest.SystemError(), saved = Guest.SavedError() };
}

internal static class NativeMethods
{
    [DllImport("KERNEL32.dll", ExactSpelling = true)]
    internal static extern uint GetCurrentThreadId();
}
