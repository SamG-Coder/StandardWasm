using System.Runtime.InteropServices;
using System.Text.Json;

if (!OperatingSystem.IsWindows() || IntPtr.Size != 4)
    throw new InvalidOperationException("This oracle requires Windows x86.");
File.WriteAllText(args[0], JsonSerializer.Serialize(new Probe().Run()));

sealed class Probe
{
    private const uint NcCreate = 0x81, NcCalcSize = 0x83, GetMinMaxInfo = 0x24, Create = 1;
    private const int MessageParent = -3;
    private const uint ParkingStyle = 0x02010000;
    private readonly List<object> _messages = [];
    private readonly List<nint> _created = [];
    private int[] _metrics = [];
    private nint _module, _class, _caption, _messageRoot, _callbackHandle;
    private uint _abort;
    private int _phase;

    internal object Run()
    {
        WindowProc procedure = Dispatch;
        _module = GetModuleHandleW(0);
        _class = Marshal.StringToHGlobalUni("StandardWasm.MessageOnly");
        _caption = Marshal.StringToHGlobalUni("Parking");
        ushort atom = RegisterClassW(new WindowClass { Module = _module, Name = _class,
            Procedure = Marshal.GetFunctionPointerForDelegate(procedure) });
        if (atom == 0) throw new InvalidOperationException("Window class registration failed.");
        try
        {
            object parking = Observe("parking", _class, ParkingStyle, 0, 0, 0, 0, 0);
            nint parent = _created[0];
            object sibling = Observe("sibling", atom, 0, 2, 3, 200, 85, 0);
            object rejected = Observe("reject", _class, ParkingStyle, 0, 0, 0, 0, NcCreate);
            object focus = CheckFocus(parent);
            return new { clr = Environment.Version.ToString(), metrics = _metrics, cases = new[] { parking, sibling, rejected }, focus };
        }
        finally
        {
            _phase = 0;
            foreach (nint hwnd in Enumerable.Reverse(_created)) DestroyWindow(hwnd);
            UnregisterClassW(_class, _module);
            Marshal.FreeHGlobal(_caption);
            Marshal.FreeHGlobal(_class);
            GC.KeepAlive(procedure);
        }
    }

    private object Observe(string name, nint className, uint style, int x, int y, int width, int height, uint abort)
    {
        _messages.Clear();
        _abort = abort;
        _callbackHandle = 0;
        _phase = 1;
        nint hwnd = CreateWindowExW(0, className, _caption, style, x, y, width, height, MessageParent, 0, _module, 123);
        int savedError = Marshal.GetLastPInvokeError();
        _phase = 0;
        if (hwnd != 0) _created.Add(hwnd);
        return new { name, success = hwnd != 0, error = hwnd == 0 ? savedError : 0, live = IsWindow(_callbackHandle) != 0,
            sameHandle = hwnd == 0 || hwnd == _callbackHandle,
            visible = IsWindowVisible(hwnd) != 0, messages = _messages.ToArray() };
    }

    private static object CheckFocus(nint hwnd)
    {
        nint initial = GetFocus();
        nint previous = SetFocus(hwnd);
        bool focused = GetFocus() == hwnd;
        SetFocus(initial);
        return new { hadFocus = initial != 0, previous = previous != 0, focused };
    }

    private nint Dispatch(nint hwnd, uint message, nint wParam, nint lParam)
    {
        if (_phase != 0)
        {
            if (_callbackHandle == 0) _callbackHandle = hwnd;
            int[] data = [];
            string? parent = null;
            string? caption = null;
            if (message == GetMinMaxInfo)
            {
                data = ReadWords(lParam, 10);
                data[0] = data[1] = 0; // ptReserved
                if (_metrics.Length == 0) _metrics = data;
            }
            else if (message is NcCreate or Create)
            {
                int[] fields = ReadWords(lParam, 12);
                data = [fields[0], fields[1] == _module ? 1 : 0, fields[2], .. fields[4..9], fields[11]];
                parent = Parent(fields[3]);
                caption = Marshal.PtrToStringUni(fields[9]);
            }
            else if (message == NcCalcSize)
                data = ReadWords(lParam, 4);
            int hwndParent = GetWindowLongW(hwnd, -8);
            if (_messageRoot == 0 && IsWindow(hwndParent) != 0) _messageRoot = hwndParent;
            bool live = IsWindow(hwnd) != 0;
            uint style = (uint)GetWindowLongW(hwnd, -16), exStyle = (uint)GetWindowLongW(hwnd, -20);
            nint result = _abort == message ? (message == Create ? -1 : 0) : DefWindowProcW(hwnd, message, wParam, lParam);
            int[] adjusted = message == NcCalcSize ? ReadWords(lParam, 4) : [];
            _messages.Add(new { message, wParam = (int)wParam, data, adjusted, parent, caption, live,
                style, exStyle,
                parentField = Parent(hwndParent) });
            return result;
        }
        return DefWindowProcW(hwnd, message, wParam, lParam);
    }

    private string Parent(nint value) => value == 0 ? "none" : value == MessageParent ? "sentinel" :
        value == _messageRoot && IsWindow(value) != 0 ? "system" : "unknown";

    private static int[] ReadWords(nint address, int count)
    {
        var values = new int[count];
        Marshal.Copy(address, values, 0, count);
        return values;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowClass
    {
        public uint Style;
        public nint Procedure;
        public int ClassExtra, WindowExtra;
        public nint Module, Icon, Cursor, Background, Menu, Name;
    }

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate nint WindowProc(nint hwnd, uint message, nint wParam, nint lParam);
    [DllImport("kernel32", ExactSpelling = true)] private static extern nint GetModuleHandleW(nint name);
    [DllImport("user32", ExactSpelling = true, SetLastError = true)] private static extern ushort RegisterClassW(in WindowClass definition);
    [DllImport("user32", ExactSpelling = true)] private static extern int UnregisterClassW(nint name, nint instance);
    [DllImport("user32", ExactSpelling = true, SetLastError = true)]
    private static extern nint CreateWindowExW(uint exStyle, nint name, nint caption, uint style, int x, int y,
        int width, int height, nint parent, nint menu, nint instance, nint param);
    [DllImport("user32", ExactSpelling = true)] private static extern nint DefWindowProcW(nint hwnd, uint message, nint wParam, nint lParam);
    [DllImport("user32", ExactSpelling = true)] private static extern int IsWindow(nint hwnd);
    [DllImport("user32", ExactSpelling = true)] private static extern int IsWindowVisible(nint hwnd);
    [DllImport("user32", ExactSpelling = true)] private static extern int GetWindowLongW(nint hwnd, int index);
    [DllImport("user32", ExactSpelling = true)] private static extern nint GetFocus();
    [DllImport("user32", ExactSpelling = true)] private static extern nint SetFocus(nint hwnd);
    [DllImport("user32", ExactSpelling = true)] private static extern int DestroyWindow(nint hwnd);
}
