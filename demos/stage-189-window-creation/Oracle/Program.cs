using System.Runtime.InteropServices;
using System.Text.Json;

if (IntPtr.Size != 4) throw new InvalidOperationException("The window creation oracle requires Windows x86.");
File.WriteAllText(args[0], JsonSerializer.Serialize(new Witness().Run()));

internal sealed class Witness
{
    private const string ClassName = "StandardWasm.Stage189.É";
    private const uint Popup = 0x80000000;
    private const int MinMaxInfo = 0x0024, NcCreate = 0x0081, NcCalcSize = 0x0083, Create = 0x0001;
    private readonly List<object> _messages = [];
    private readonly List<nint> _successfulWindows = [];
    private int[] _metrics = [];
    private nint _name, _caption, _module, _first, _next;
    private ushort _atom;
    private Request _active;
    private bool _recording;
    private bool _replaced;
    private bool _nestedSeparated;

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowClass
    {
        public uint Style;
        public nint Procedure;
        public int ClassExtra, WindowExtra;
        public nint Module, Icon, Cursor, Background, Menu, Name;
    }

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int WindowProcedure(nint hwnd, uint message, int wparam, nint lparam);
    [DllImport("kernel32", ExactSpelling = true)] private static extern nint GetModuleHandleW(nint name);
    [DllImport("user32", ExactSpelling = true, SetLastError = true)] private static extern ushort RegisterClassW(in WindowClass definition);
    [DllImport("user32", ExactSpelling = true, SetLastError = true)] private static extern int UnregisterClassW(nint name, nint instance);
    [DllImport("user32", ExactSpelling = true, SetLastError = true)]
    private static extern nint CreateWindowExW(uint exStyle, nint name, nint caption, uint style, int x, int y,
        int width, int height, nint parent, nint menu, nint instance, nint parameter);
    [DllImport("user32", ExactSpelling = true, SetLastError = true)]
    private static extern int SetWindowLongW(nint hwnd, int index, int value);
    [DllImport("user32", ExactSpelling = true)] private static extern int GetWindowLongW(nint hwnd, int index);
    [DllImport("user32", ExactSpelling = true)] private static extern int IsWindow(nint hwnd);
    [DllImport("user32", ExactSpelling = true)] private static extern int DestroyWindow(nint hwnd);

    private sealed record Request(string Key, uint Style, bool Atom, int X, int Y, int Width, int Height, int Param,
        int Abort = 0, bool Resize = false, bool Nest = false, bool Caption = true)
    {
        public nint Handle;
        public nint Creation;
    }

    internal object Run()
    {
        WindowProcedure first = (hwnd, message, wparam, lparam) => Dispatch(1, hwnd, (int)message, wparam, lparam);
        WindowProcedure next = (hwnd, message, wparam, lparam) => Dispatch(2, hwnd, (int)message, wparam, lparam);
        _first = Marshal.GetFunctionPointerForDelegate(first);
        _next = Marshal.GetFunctionPointerForDelegate(next);
        _name = Marshal.StringToHGlobalUni(ClassName);
        _caption = Marshal.StringToHGlobalUni("É漢");
        _module = GetModuleHandleW(0);
        var registration = new WindowClass { Procedure = _first, WindowExtra = 8, Module = _module, Name = _name };
        _atom = RegisterClassW(registration);
        if (_atom == 0) throw new InvalidOperationException("The x86 class failed to register.");
        try
        {
            var cases = new[]
            {
                RunCase(new Request("overlapped", 0, false, 13, 17, 24, 25, 0x1234, Resize: true)),
                RunCase(new Request("popup", Popup, true, -13, 17, 161, 103, -2, Resize: true)),
                RunCase(new Request("reject-nccreate", Popup, false, 13, 17, 161, 103, 0, Abort: NcCreate)),
                RunCase(new Request("reject-create", Popup, false, 13, 17, 161, 103, 0, Abort: Create)),
                RunCase(new Request("nested-outer", Popup, false, 13, 17, 161, 103, 0x1111, Nest: true)),
                RunCase(new Request("null-caption", Popup, false, 13, 17, 161, 103, 0, Caption: false))
            };
            nint missing = Marshal.StringToHGlobalUni("Stage189 missing");
            int[] unknownClass;
            try
            {
                nint hwnd = CreateWindowExW(0, missing, 0, Popup, 0, 0, 5, 5, 0, 0, _module, 0);
                unknownClass = [hwnd.ToInt32(), Marshal.GetLastPInvokeError()];
            }
            finally { Marshal.FreeHGlobal(missing); }
            return new { clr = Environment.Version.ToString(), metrics = _metrics, unknownClass,
                outcomes = new { cases, nestedSeparated = _nestedSeparated } };
        }
        finally
        {
            _recording = false;
            foreach (nint hwnd in _successfulWindows) DestroyWindow(hwnd);
            UnregisterClassW(_name, _module);
            Marshal.FreeHGlobal(_caption);
            Marshal.FreeHGlobal(_name);
            GC.KeepAlive(first);
            GC.KeepAlive(next);
        }
    }

    private object RunCase(Request request)
    {
        _messages.Clear();
        _replaced = true;
        _recording = true;
        (nint hwnd, int error) = RunWindow(request);
        _recording = false;
        return new { key = request.Key, success = hwnd != 0, error, live = IsWindow(request.Handle) != 0,
            handleMatches = hwnd == 0 || hwnd == request.Handle, replaced = _replaced, messages = _messages.ToArray() };
    }

    private (nint, int) RunWindow(Request request)
    {
        Request previous = _active;
        _active = request;
        try
        {
            nint hwnd = CreateWindowExW(0, request.Atom ? _atom : _name, request.Caption ? _caption : 0,
                request.Style, request.X, request.Y, request.Width, request.Height, 0, 0, _module, request.Param);
            int error = Marshal.GetLastPInvokeError();
            if (hwnd != 0) _successfulWindows.Add(hwnd);
            return (hwnd, error);
        }
        finally { _active = previous; }
    }

    private int Dispatch(int target, nint hwnd, int message, int wparam, nint lparam)
    {
        if (!_recording) return message == NcCreate ? 1 : 0;
        Request request = _active;
        if (request.Handle == 0) request.Handle = hwnd;
        int[] data = [];
        string caption = null, className = null;
        if (message == MinMaxInfo)
        {
            data = ReadInts(lparam, 10);
            _metrics = data;
            if (request.Resize) { Marshal.WriteInt32(lparam, 24, 72); Marshal.WriteInt32(lparam, 28, 60); }
        }
        else if (message is NcCreate or Create)
        {
            if (message == NcCreate) request.Creation = lparam;
            int[] fields = ReadInts(lparam, 12);
            caption = fields[9] == 0 ? null : Marshal.PtrToStringUni(fields[9]);
            className = (uint)fields[10] <= ushort.MaxValue ? "atom" : Marshal.PtrToStringUni(fields[10]);
            data = [fields[0], fields[1] == _module ? 1 : 0, fields[2], fields[3], fields[4], fields[5], fields[6],
                fields[7], fields[8], fields[11], lparam == request.Creation ? 1 : 0];
            // Windows restores the original parameter when it sends WM_CREATE.
            if (message == NcCreate) Marshal.WriteInt32(lparam, 0, -9876);
        }
        else if (message == NcCalcSize)
        {
            data = ReadInts(lparam, 4);
            if (request.Resize && request.Style == Popup)
            {
                Marshal.WriteInt32(lparam, 0, data[0] + 3);
                Marshal.WriteInt32(lparam, 8, data[2] - 5);
            }
        }
        else if (message is 0x0003 or 0x0005) data = [lparam.ToInt32()];
        _messages.Add(new { key = request.Key, target, message, wparam, data, caption, className,
            live = IsWindow(hwnd) != 0, sameHandle = request.Handle == hwnd, style = (uint)GetWindowLongW(hwnd, -16),
            exStyle = (uint)GetWindowLongW(hwnd, -20) });
        if (target == 1) _replaced &= SetWindowLongW(hwnd, -4, (int)_next) == _first;
        if (message == NcCreate && request.Nest)
        {
            var inner = new Request("nested-inner", Popup, true, 20, 21, 42, 43, 0x2222);
            var (nested, _) = RunWindow(inner);
            _nestedSeparated = nested != 0 && inner.Creation != request.Creation && Marshal.ReadInt32(lparam) == -9876;
        }
        if (message == request.Abort) return message == Create ? -1 : 0;
        return message == NcCreate ? 1 : 0;
    }

    private static int[] ReadInts(nint pointer, int count)
    {
        var data = new int[count];
        Marshal.Copy(pointer, data, 0, count);
        return data;
    }
}
