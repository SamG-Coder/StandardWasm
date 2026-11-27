using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows.Forms;

nint user32 = NativeLibrary.Load("user32.dll");
try
{
    nint module = NativeMethods.GetModuleHandleW("UsEr32");
    nint procedure = NativeMethods.GetProcAddress(module, "DefWindowProcW");
    nint missingModule = NativeMethods.GetModuleHandleW("StandardWasm-Missing-Window-Fixture.dll");
    int moduleError = Marshal.GetLastPInvokeError();
    nint missingProcedure = NativeMethods.GetProcAddress(module, "defwindowprocw");
    int procedureError = Marshal.GetLastPInvokeError();

    var property = typeof(NativeWindow).GetProperty("DefaultWindowProc", BindingFlags.NonPublic | BindingFlags.Static)!;
    var readOriginal = property.GetMethod!.CreateDelegate<Func<nint>>();
    nint original = readOriginal();
    bool nominal;
    unsafe
    {
        fixed (byte* name = "DefWindowProcW\0"u8)
            nominal = WindowProcBindingFixture.Lookup(user32, name) == procedure;
    }
    var result = new
    {
        architecture = RuntimeInformation.ProcessArchitecture.ToString(),
        winForms = typeof(NativeWindow).Assembly.GetName().Version!.ToString(),
        fixtureAssembly = typeof(WindowProcBindingFixture).Assembly.Location,
        fixtureRow = typeof(WindowProcBindingFixture).GetMethod(nameof(WindowProcBindingFixture.Lookup))!.MetadataToken & 0x00ffffff,
        dispatchRow = typeof(WindowProcBindingFixture).GetMethod(nameof(WindowProcBindingFixture.Dispatch))!.MetadataToken & 0x00ffffff,
        nominal,
        dispatch = WindowProcedureOracle.Observe(),
        nominalDispatch = WindowProcedureOracle.Observe(nominal: true),
        lookup = new
        {
            sameModule = module == user32,
            sameProcedure = procedure == NativeLibrary.GetExport(user32, "DefWindowProcW"),
            missingModule = missingModule == 0 ? 0 : 1, moduleError,
            missingProcedure = missingProcedure == 0 ? 0 : 1, procedureError
        },
        original = new { matches = original == procedure, cached = readOriginal() == original }
    };
    File.WriteAllText(args[0], JsonSerializer.Serialize(result));
}
finally { NativeLibrary.Free(user32); }

internal static class NativeMethods
{
    [DllImport("kernel32.dll", ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern nint GetModuleHandleW(string name);

    [DllImport("kernel32.dll", ExactSpelling = true, CharSet = CharSet.Ansi, SetLastError = true)]
    internal static extern nint GetProcAddress(nint module, string name);
}
