using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text.Json;

if (IntPtr.Size != 4) throw new InvalidOperationException("Use the Windows x86 CLR.");
File.WriteAllText(args[0], JsonSerializer.Serialize(Oracle.Run()));

internal static class Oracle
{
    internal static object Run()
    {
        const int system = 0x5a5a, saved = 0x6217;
        bool measuring = false;
        int paintMessages = 0;
        Win.Procedure callback = (hwnd, message, wparam, lparam) =>
        {
            if (measuring && message is 0x000f or 0x0014 or 0x0085) paintMessages++;
            return Win.DefWindowProcW(hwnd, message, wparam, lparam);
        };
        nint instance = Win.GetModuleHandleW(null);
        string className = "StandardWasm.HiddenInvalidation." + Environment.ProcessId;
        var definition = new Win.WindowClass { Instance = instance, Name = className,
            Callback = Marshal.GetFunctionPointerForDelegate(callback) };
        if (Win.RegisterClassW(ref definition) == 0) throw new Win32Exception(Marshal.GetLastPInvokeError());
        nint window = 0;
        try
        {
            window = Win.CreateWindowExW(0, className, "", 0x80000000, 24, 30, 180, 100, 0, 0, instance, 0);
            if (window == 0) throw new Win32Exception(Marshal.GetLastPInvokeError());
            measuring = true;
            Raw invalidation = new(window, system, saved);
            var cases = new[]
            {
                invalidation.Call(0, null),
                invalidation.Call(1, null),
                invalidation.Call(0, [12, 9, 64, 41]),
                invalidation.Call(-1, [-25, -19, 30, 28]),
                invalidation.Call(0, [220, 110, 260, 140]),
                invalidation.Call(1, [20, 20, 20, 20]),
                invalidation.Call(7, [41, 35, 12, 9]),
                invalidation.Call(0, [108, 62, 132, 83]),
                invalidation.Call(1, [1, 1, 8, 9]),
            };
            measuring = false;
            nint retired = window;
            if (!Win.DestroyWindow(window)) throw new Win32Exception(Marshal.GetLastPInvokeError());
            window = 0;
            Guest.SetErrors(system, saved);
            int staleResult = Guest.Invalidate(retired, 0, 1);
            var stale = new { result = staleResult, error = Guest.Error(), saved = Guest.SavedError() };
            return new { system, saved, cases, paintMessages, stale };
        }
        finally
        {
            if (window != 0) Win.DestroyWindow(window);
            Win.UnregisterClassW(className, instance);
            GC.KeepAlive(callback);
        }
    }

    private sealed class Raw(nint hwnd, int system, int saved)
    {
        internal unsafe object Call(int erase, int[]? rect)
        {
            int* rectangle = stackalloc int[4];
            if (rect is not null)
                for (int i = 0; i < 4; i++) rectangle[i] = rect[i];
            Guest.SetErrors(system, saved);
            int result = Guest.Invalidate(hwnd, rect is null ? 0 : (nint)rectangle, erase);
            int error = Guest.Error(), savedError = Guest.SavedError();
            bool unchanged = rect is null ||
                (rectangle[0] == rect[0] && rectangle[1] == rect[1] && rectangle[2] == rect[2] && rectangle[3] == rect[3]);
            return new { erase, rect, result, error, saved = savedError, unchanged,
                pending = Win.GetUpdateRect(hwnd, 0, false),
                queued = Win.PeekMessageW(out _, hwnd, 0x000f, 0x000f, 0) };
        }
    }
}

internal static class Win
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct Message
    {
        internal nint Window;
        internal uint Id;
        internal nint WParam, LParam;
        internal uint Time;
        internal int X, Y;
        internal uint Private;
    }
    [DllImport("user32.dll", ExactSpelling = true)]
    internal static extern bool PeekMessageW(out Message message, nint hwnd, uint min, uint max, uint remove);

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
    [DllImport("user32.dll", ExactSpelling = true)]
    internal static extern nint DefWindowProcW(nint hwnd, uint message, nint wparam, nint lparam);
    [DllImport("user32.dll", ExactSpelling = true, SetLastError = true, CharSet = CharSet.Unicode)]
    internal static extern ushort RegisterClassW(ref WindowClass definition);
    [DllImport("user32.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
    internal static extern bool UnregisterClassW(string name, nint instance);
    [DllImport("user32.dll", ExactSpelling = true, SetLastError = true, CharSet = CharSet.Unicode)]
    internal static extern nint CreateWindowExW(uint extended, string name, string caption, uint style, int x, int y,
        int width, int height, nint parent, nint menu, nint instance, nint data);
    [DllImport("user32.dll", ExactSpelling = true)]
    internal static extern bool DestroyWindow(nint hwnd);
    [DllImport("user32.dll", ExactSpelling = true)]
    internal static extern bool GetUpdateRect(nint hwnd, nint rectangle, bool erase);
}
