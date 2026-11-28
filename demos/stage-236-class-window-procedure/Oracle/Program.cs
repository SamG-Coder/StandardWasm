using System.Runtime.InteropServices;
using System.Text.Json;

static unsafe class Oracle
{
    private const int ClassProcedure = -24, WindowProcedureIndex = -4;
    private const int SystemSentinel = 17185, PInvokeSentinel = 25090;
    private const uint Echo = 0x8042;
    private static readonly Procedure First = (window, message, word, value) =>
        message == Echo ? 101 : DefWindowProc(window, message, word, value);
    private static readonly Procedure Second = (window, message, word, value) =>
        message == Echo ? 202 : DefWindowProc(window, message, word, value);

    public static void Main(string[] args)
    {
        if (IntPtr.Size != 4) throw new InvalidOperationException("Use the Windows x86 CLR.");
        nint module = GetModuleHandle(0);
        nint original = Marshal.GetFunctionPointerForDelegate(First);
        nint replacement = Marshal.GetFunctionPointerForDelegate(Second);
        nint name = Marshal.StringToHGlobalUni("StandardWasm.Stage236." + Guid.NewGuid().ToString("N"));
        var windows = new List<nint>();
        try
        {
            var definition = new WindowClass { Procedure = original, Module = module, Name = name };
            int atom = Guest.Register((nint)(&definition));
            if (atom == 0) throw new InvalidOperationException("RegisterClassW failed.");
            nint a = Open(name, module, windows), b = Open(atom, module, windows);
            int[] before = [Message(a), Message(b)];
            // Warm the exact import before reading its raw last-error behavior.
            Guest.SetClass(a, ClassProcedure, original);
            bool oldWindowWasOriginal = Guest.SetWindow(a, WindowProcedureIndex, replacement) == original;
            var changed = SetAndCapture(a, ClassProcedure, replacement);
            int[] after = [Message(a), Message(b)];
            nint c = Open(name, module, windows);
            var info = new WindowClass();
            bool infoReadsChangedProcedure = Guest.ClassInfo(module, name, (nint)(&info)) != 0 && info.Procedure == replacement;
            bool oldWindowProcedureUnchanged = Guest.GetWindow(b, WindowProcedureIndex) == original;
            var restored = SetAndCapture(b, ClassProcedure, original);
            nint d = Open(atom, module, windows);
            var badIndex = SetAndCapture(a, -999, original);
            var badWindow = SetAndCapture(0, ClassProcedure, original);
            File.WriteAllText(args[0], JsonSerializer.Serialize(new
            {
                clr = Environment.Version.ToString(),
                results = new
                {
                    before, after, newAfterChange = Message(c), newAfterRestore = Message(d), cAfterRestore = Message(c),
                    oldWindowWasOriginal, oldWindowProcedureUnchanged, infoReadsChangedProcedure,
                    classReturnCorrect = changed.Value == original, classRestoreReturnCorrect = restored.Value == replacement,
                    changedErrors = new[] { changed.System, changed.PInvoke }, restoredErrors = new[] { restored.System, restored.PInvoke },
                    badIndex = new { zero = badIndex.Value == 0, system = badIndex.System, saved = badIndex.PInvoke },
                    badWindow = new { zero = badWindow.Value == 0, system = badWindow.System, saved = badWindow.PInvoke }
                }
            }));
        }
        finally
        {
            for (int index = windows.Count - 1; index >= 0; index--) DestroyWindow(windows[index]);
            UnregisterClass(name, module);
            Marshal.FreeHGlobal(name);
        }
    }

    private static (nint Value, int System, int PInvoke) SetAndCapture(nint window, int index, nint value)
    {
        Guest.SetErrors(SystemSentinel, PInvokeSentinel);
        nint previous = Guest.SetClass(window, index, value);
        return (previous, Guest.SystemError(), Guest.SavedError());
    }

    private static int Message(nint window) => (int)SendMessage(window, Echo, 0, 0);

    private static nint Open(nint kind, nint module, List<nint> windows)
    {
        nint window = Guest.Create(kind, module);
        if (window == 0) throw new InvalidOperationException("CreateWindowExW failed.");
        windows.Add(window);
        return window;
    }

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate nint Procedure(nint hwnd, uint message, nuint word, nint value);

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowClass
    {
        public uint Style;
        public nint Procedure;
        public int ClassExtra, WindowExtra;
        public nint Module, Icon, Cursor, Background, Menu, Name;
    }

    [DllImport("kernel32.dll", EntryPoint = "GetModuleHandleW", ExactSpelling = true)]
    private static extern nint GetModuleHandle(nint module);
    [DllImport("user32.dll", EntryPoint = "DefWindowProcW", ExactSpelling = true)]
    private static extern nint DefWindowProc(nint window, uint message, nuint word, nint value);
    [DllImport("user32.dll", EntryPoint = "SendMessageW", ExactSpelling = true)]
    private static extern nint SendMessage(nint window, uint message, nuint word, nint value);
    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern bool DestroyWindow(nint window);
    [DllImport("user32.dll", EntryPoint = "UnregisterClassW", ExactSpelling = true)]
    private static extern bool UnregisterClass(nint name, nint module);
}
