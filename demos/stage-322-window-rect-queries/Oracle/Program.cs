using System.Runtime.InteropServices;
using System.Text.Json;

if (!OperatingSystem.IsWindows() || IntPtr.Size != 4)
    throw new InvalidOperationException("This oracle requires Windows x86.");
File.WriteAllText(args[0], JsonSerializer.Serialize(new Oracle().Run()));

sealed class Oracle
{
    private readonly List<object> _events = [];
    private int[] _metrics = [];
    private bool _record;

    internal object Run()
    {
        WindowProc callback = Dispatch;
        nint module = GetModuleHandleW(0);
        nint name = Marshal.StringToHGlobalUni("StandardWasm.RectQueries");
        ushort atom = RegisterClassW(new WindowClass { Module = module, Name = name,
            Procedure = Marshal.GetFunctionPointerForDelegate(callback) });
        if (atom == 0) throw new InvalidOperationException("Window class registration failed.");
        try
        {
            var requests = new[] {
                new Request(0x02cf0000, 0x050100, 18, 27, 376, 183),
                new Request(0x80000000, 0, -5, 11, 100, 44)
            };
            var outcomes = requests.Select(request => Observe(request, atom, module)).ToArray();
            return new { assembly = typeof(RectangleGuest).Assembly.Location, clr = Environment.Version.ToString(),
                metrics = _metrics, requests, outcomes, invalid = Query(-1) };
        }
        finally
        {
            UnregisterClassW(name, module);
            Marshal.FreeHGlobal(name);
            GC.KeepAlive(callback);
        }
    }

    private object Observe(Request request, ushort atom, nint module)
    {
        _events.Clear();
        _record = true;
        nint hwnd = CreateWindowExW(request.ExStyle, atom, 0, request.Style, request.X, request.Y,
            request.Width, request.Height, 0, 0, module, 0);
        _record = false;
        if (hwnd == 0) throw new InvalidOperationException("Native fixture window was rejected.");
        object created = Query(hwnd);
        if (DestroyWindow(hwnd) == 0) throw new InvalidOperationException("Could not destroy the fixture window.");
        return new { callbacks = _events.ToArray(), created, retired = Query(hwnd) };
    }

    private nint Dispatch(nint hwnd, uint message, nint wParam, nint lParam)
    {
        if (!_record || message is not (0x24 or 0x81 or 0x83 or 1))
            return DefWindowProcW(hwnd, message, wParam, lParam);
        if (message == 0x24 && _metrics.Length == 0)
        {
            _metrics = new int[10];
            Marshal.Copy(lParam, _metrics, 0, 10);
            _metrics[0] = _metrics[1] = 0;
        }
        object before = Query(hwnd);
        nint result = DefWindowProcW(hwnd, message, wParam, lParam);
        _events.Add(new { message, before });
        return result;
    }

    private static object Query(nint hwnd) => new { window = Call(RectangleGuest.Window, hwnd), client = Call(RectangleGuest.Client, hwnd) };

    private static unsafe object Call(Func<nint, nint, int> query, nint hwnd)
    {
        int* output = stackalloc int[5] { 11, 22, 33, 44, 55 };
        int result = query(hwnd, (nint)output);
        return new { result, error = RectangleGuest.SavedError(),
            words = new[] { output[0], output[1], output[2], output[3], output[4] } };
    }

    private sealed record Request(uint Style, uint ExStyle, int X, int Y, int Width, int Height);
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
    [DllImport("user32", ExactSpelling = true, SetLastError = true)] private static extern int UnregisterClassW(nint name, nint instance);
    [DllImport("user32", ExactSpelling = true, SetLastError = true)]
    private static extern nint CreateWindowExW(uint exStyle, nint name, nint caption, uint style, int x, int y,
        int width, int height, nint parent, nint menu, nint instance, nint param);
    [DllImport("user32", ExactSpelling = true)] private static extern nint DefWindowProcW(nint hwnd, uint message, nint wParam, nint lParam);
    [DllImport("user32", ExactSpelling = true)] private static extern int DestroyWindow(nint hwnd);
}
