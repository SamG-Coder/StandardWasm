using System.Runtime.InteropServices;
using System.Text.Json;

if (!OperatingSystem.IsWindows() || IntPtr.Size != 4)
    throw new InvalidOperationException("This oracle requires Windows x86.");
File.WriteAllText(args[0], JsonSerializer.Serialize(new Witness().Run()));

sealed class Witness
{
    private const uint NcCreate = 0x81, NcCalcSize = 0x83, GetMinMaxInfo = 0x24, Create = 1;
    private const int DefaultPosition = int.MinValue;
    private readonly List<object> _messages = [];
    private readonly List<nint> _windows = [];
    private int[] _metrics = [];
    private nint _seen, _module;
    private bool _record, _reject;

    internal object Run()
    {
        WindowProc callback = Dispatch;
        _module = GetModuleHandleW(0);
        nint name = Marshal.StringToHGlobalUni("StandardWasm.OverlappedCreation");
        nint caption = Marshal.StringToHGlobalUni("Overlapped creation");
        ushort atom = RegisterClassW(new WindowClass { Module = _module, Name = name,
            Procedure = Marshal.GetFunctionPointerForDelegate(callback) });
        if (atom == 0) throw new InvalidOperationException("Could not register window fixture.");
        try
        {
            var requests = new[] {
                new Request("winforms", 0x02cf0000, 0x050100, DefaultPosition, DefaultPosition, 376, 183),
                new Request("explicit", 0x00cf0000, 0, -40, 28, 240, 112),
                new Request("default-x", 0x02cf0000, 0x050100, DefaultPosition, 71, 350, 200),
                new Request("reject", 0x02cf0000, 0x050100, 12, 15, 220, 100, true)
            };
            var outcomes = requests.Select(request => Observe(request, atom, caption)).ToArray();
            return new { clr = Environment.Version.ToString(), metrics = _metrics, requests, outcomes };
        }
        finally
        {
            foreach (nint window in _windows) DestroyWindow(window);
            UnregisterClassW(name, _module);
            Marshal.FreeHGlobal(caption);
            Marshal.FreeHGlobal(name);
            GC.KeepAlive(callback);
        }
    }

    private object Observe(Request request, nint atom, nint caption)
    {
        _messages.Clear();
        _seen = 0;
        _reject = request.Reject;
        _record = true;
        nint handle = CreateWindowExW(request.ExStyle, atom, caption, request.Style, request.X, request.Y,
            request.Width, request.Height, 0, 0, _module, 123);
        int error = handle == 0 ? Marshal.GetLastPInvokeError() : 0;
        _record = false;
        int[] window = [], client = [], clientOrigin = [];
        if (handle != 0)
        {
            _windows.Add(handle);
            if (GetWindowRect(handle, out Rect outer) == 0 || GetClientRect(handle, out Rect inner) == 0)
                throw new InvalidOperationException("Could not read window rectangles.");
            var point = new Point();
            if (ClientToScreen(handle, ref point) == 0) throw new InvalidOperationException("Could not map client origin.");
            window = [outer.Left, outer.Top, outer.Right, outer.Bottom];
            client = [inner.Left, inner.Top, inner.Right, inner.Bottom];
            clientOrigin = [point.X, point.Y];
        }
        return new { success = handle != 0, error, visible = IsWindowVisible(handle) != 0,
            live = IsWindow(_seen) != 0, window, client, clientOrigin, messages = _messages.ToArray() };
    }

    private nint Dispatch(nint hwnd, uint message, nint wParam, nint lParam)
    {
        if (!_record) return DefWindowProcW(hwnd, message, wParam, lParam);
        if (_seen == 0) _seen = hwnd;
        int[] data = [];
        if (message == GetMinMaxInfo)
        {
            data = Read(lParam, 10);
            data[0] = data[1] = 0; // ptReserved
            if (_metrics.Length == 0) _metrics = data;
        }
        else if (message is NcCreate or Create)
        {
            var fields = Read(lParam, 12);
            data = [fields[0], fields[1] == _module ? 1 : 0, .. fields[2..9], fields[11]];
        }
        else if (message == NcCalcSize)
            data = Read(lParam, 4);

        uint style = (uint)GetWindowLongW(hwnd, -16), exStyle = (uint)GetWindowLongW(hwnd, -20);
        long result = _reject && message == NcCreate ? 0 : DefWindowProcW(hwnd, message, wParam, lParam);
        _messages.Add(new { message, wParam = (int)wParam, data, result,
            adjusted = message == NcCalcSize ? Read(lParam, 4) : [], style, exStyle });
        return (nint)result;
    }

    private static int[] Read(nint address, int length)
    {
        var result = new int[length];
        Marshal.Copy(address, result, 0, length);
        return result;
    }

    private sealed record Request(string Name, uint Style, uint ExStyle, int X, int Y, int Width, int Height, bool Reject = false);
    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct Point { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] private struct WindowClass
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
    [DllImport("user32", ExactSpelling = true)] private static extern int GetWindowRect(nint hwnd, out Rect bounds);
    [DllImport("user32", ExactSpelling = true)] private static extern int GetClientRect(nint hwnd, out Rect bounds);
    [DllImport("user32", ExactSpelling = true)] private static extern int ClientToScreen(nint hwnd, ref Point point);
    [DllImport("user32", ExactSpelling = true)] private static extern int DestroyWindow(nint hwnd);
}
