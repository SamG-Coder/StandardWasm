using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

if (IntPtr.Size != 4) throw new InvalidOperationException("Use the Windows x86 CLR.");
_ = Win.GetDesktopWindow();
nint module = Win.GetModuleHandleW("user32.dll");
nint sameModule = Win.GetModuleHandleW("USER32.DLL");
nint address = Win.GetProcAddress(module, "DefWindowProcW");
nint sameAddress = Win.GetProcAddress(sameModule, "DefWindowProcW");
nint absent = Win.GetProcAddress(module, "defwindowprocw");
int wrongCaseError = Marshal.GetLastPInvokeError();
if (module == 0 || address == 0) throw new Win32Exception(wrongCaseError);

var messages = new List<object>();
bool creating = true;
Win.Procedure callback = (window, message, wparam, lparam) =>
{
    int fields = message is 0x81 or 1 ? 12 : message == 0x83 ? 4 : 0;
    int[] before = new int[fields];
    if (fields != 0) Marshal.Copy(lparam, before, 0, fields);
    nint result = Win.CallWindowProcW(address, window, message, wparam, lparam);
    int[] after = new int[fields];
    if (fields != 0) Marshal.Copy(lparam, after, 0, fields);
    if (creating)
        messages.Add(new { message, wparam = (int)wparam, packed = fields == 0 ? (int)lparam : 0,
            result = (int)result, unchanged = before.SequenceEqual(after) });
    return result;
};

const string caption = "StandardWasm \u03bb";
nint instance = Win.GetModuleHandleW(null);
string name = "StandardWasm.DefaultProcedure." + Environment.ProcessId;
var definition = new Win.WindowClass
{
    Name = name, Instance = instance, Callback = Marshal.GetFunctionPointerForDelegate(callback)
};
if (Win.RegisterClassW(ref definition) == 0) throw new Win32Exception(Marshal.GetLastPInvokeError());
nint hwnd = 0;
try
{
    hwnd = Win.CreateWindowExW(0, name, caption, 0x80000000, 110, 70, 320, 144, 0, 0, instance, 0x1234567);
    creating = false;
    if (hwnd == 0) throw new Win32Exception(Marshal.GetLastPInvokeError());
    var text = new StringBuilder(80);
    Win.GetWindowTextW(hwnd, text, text.Capacity);
    var result = new
    {
        moduleLoaded = module != 0, moduleStable = module == sameModule, procedureStable = address == sameAddress,
        wrongCase = (int)absent, wrongCaseError, title = text.ToString(), messages
    };
    File.WriteAllText(args[0], JsonSerializer.Serialize(result));
}
finally
{
    if (hwnd != 0) Win.DestroyWindow(hwnd);
    Win.UnregisterClassW(name, instance);
    GC.KeepAlive(callback);
}

internal static class Win
{
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    internal delegate nint Procedure(nint hwnd, uint message, nint wparam, nint lparam);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct WindowClass
    {
        internal uint Style;
        internal nint Callback;
        internal int ClassExtra, WindowExtra;
        internal nint Instance, Icon, Cursor, Background;
        internal string? Menu, Name;
    }

    [DllImport("kernel32.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
    internal static extern nint GetModuleHandleW(string? name);
    [DllImport("kernel32.dll", ExactSpelling = true, CharSet = CharSet.Ansi, SetLastError = true)]
    internal static extern nint GetProcAddress(nint module, string name);
    [DllImport("user32.dll", ExactSpelling = true)]
    internal static extern nint GetDesktopWindow();
    [DllImport("user32.dll", ExactSpelling = true)]
    internal static extern nint CallWindowProcW(nint procedure, nint hwnd, uint message, nint wparam, nint lparam);
    [DllImport("user32.dll", ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern ushort RegisterClassW(ref WindowClass definition);
    [DllImport("user32.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
    internal static extern bool UnregisterClassW(string name, nint instance);
    [DllImport("user32.dll", ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern nint CreateWindowExW(uint extended, string name, string caption, uint style, int x, int y,
        int width, int height, nint parent, nint menu, nint instance, nint data);
    [DllImport("user32.dll", ExactSpelling = true)]
    internal static extern bool DestroyWindow(nint hwnd);
    [DllImport("user32.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
    internal static extern int GetWindowTextW(nint hwnd, StringBuilder text, int capacity);
}
