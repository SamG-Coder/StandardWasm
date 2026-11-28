using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using SendMessageTimeoutFixture;

if (IntPtr.Size != 4) throw new InvalidOperationException("Expected Windows x86 CLR.");
const uint echo = 0x8042, slow = 0x8043, destroy = 0x8044, changeError = 0x8045, nested = 0x8046;
int deliveries = 0;
Native.WindowProc procedure = (hwnd, message, wParam, lParam) =>
{
    switch (message)
    {
        case echo: break;
        case slow: Thread.Sleep(20); break;
        case destroy: Native.DestroyWindow(hwnd); break;
        case changeError: Marshal.SetLastSystemError(333); break;
        case nested: WindowSender.SendWindow(hwnd, (int)echo, 1, 2, 0, 0, 0, 0); break;
        default: return Native.DefWindowProcW(hwnd, message, wParam, lParam);
    }
    deliveries++;
    return (nint)wParam ^ lParam;
};
const string className = "StandardWasmSendTimeout";
var module = Native.GetModuleHandleW(0);
if (Native.RegisterClassW(new Native.WindowClass { Instance = module, Name = className, Procedure = procedure }) == 0)
    throw new InvalidOperationException("RegisterClassW failed.");
nint Open()
{
    var result = Native.CreateWindowExW(0, className, "", 0, 0, 0, 20, 20, -3, 0, module, 0);
    return result != 0 ? result : throw new InvalidOperationException("CreateWindowExW failed.");
}
var windows = new Dictionary<string, nint>
{
    ["window"] = Open(), ["closed"] = Open(), ["destroyPlain"] = Open(), ["destroyFlag"] = Open(),
    ["null"] = 0, ["invalid"] = -99
};
Native.DestroyWindow(windows["closed"]);
var output = Marshal.AllocHGlobal(4);
try
{
    var operations = new List<object>();
    Observe("window", echo, unchecked((int)0xf1234567), -9, 0, 1, true, 0);
    Observe("window", echo, unchecked((int)0xf1234567), 0, 0, 1, true, 0);
    Observe("window", echo, 25, 25, 3, 0, true, 1);
    Observe("window", slow, 7, 9, 2, 1, true, 0);
    Observe("window", echo, 3, 5, 8, 0, false, 0);
    Observe("window", changeError, 4, 6, 0, 1, true, 1);
    Observe("window", nested, 4, 6, 1, 0, true, 0);
    Observe("null", echo, 1, 2, 0, 1, true, 0);
    Observe("invalid", echo, 1, 2, 0, 1, true, 1);
    Observe("closed", echo, 1, 2, 0, 1, true, 0);
    Observe("destroyPlain", destroy, 5, 11, 0, 1, true, 0);
    Observe("destroyFlag", destroy, 5, 11, 32, 1, true, 0);
    File.WriteAllText(args[0], JsonSerializer.Serialize(new
    {
        clr = Environment.Version.ToString(),
        assembly = typeof(WindowSender).Assembly.Location,
        operations
    }));

    void Observe(string target, uint message, int wParam, int lParam, int flags, int timeout, bool write, int capture)
    {
        deliveries = 0;
        Marshal.WriteInt32(output, 0x13572468);
        WindowSender.SetErrors(741, 789);
        long started = Stopwatch.GetTimestamp();
        int status = WindowSender.SendWindow(windows[target], (int)message, wParam, lParam, flags, timeout, write ? output : 0, capture);
        int system = WindowSender.SystemError(), saved = WindowSender.SavedError();
        if (message == slow && (status == 0 || Stopwatch.GetElapsedTime(started).TotalMilliseconds < 10))
            throw new InvalidOperationException("The same-queue send failed to wait for its slow window procedure.");
        operations.Add(new { target, message, wParam, lParam, flags, timeout, write, capture,
            expected = new { status, system, saved, result = Marshal.ReadInt32(output), deliveries } });
    }
}
finally
{
    Marshal.FreeHGlobal(output);
    Native.DestroyWindow(windows["window"]);
    Native.UnregisterClassW(className, module);
    GC.KeepAlive(procedure);
}

static class Native
{
    internal delegate nint WindowProc(nint window, uint message, nuint wParam, nint lParam);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal sealed class WindowClass
    {
        internal uint Style;
        internal WindowProc Procedure;
        internal int ClassExtra, WindowExtra;
        internal nint Instance, Icon, Cursor, Background;
        internal string Menu, Name;
    }

    [DllImport("KERNEL32.dll", ExactSpelling = true)]
    internal static extern nint GetModuleHandleW(nint name);
    [DllImport("USER32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    internal static extern ushort RegisterClassW(WindowClass windowClass);
    [DllImport("USER32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    internal static extern bool UnregisterClassW(string name, nint instance);
    [DllImport("USER32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    internal static extern nint CreateWindowExW(uint extended, string name, string title, uint style,
        int x, int y, int width, int height, nint parent, nint menu, nint module, nint parameter);
    [DllImport("USER32.dll", ExactSpelling = true)]
    internal static extern bool DestroyWindow(nint window);
    [DllImport("USER32.dll", ExactSpelling = true)]
    internal static extern nint DefWindowProcW(nint window, uint message, nuint wParam, nint lParam);
}
