using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using WindowAccessibilityEvents;

if (IntPtr.Size != 4) throw new InvalidOperationException("Expected Windows x86 CLR.");
var window = Native.CreateWindowExW(0, "STATIC", "", 0, 0, 0, 24, 24, 0, 0, 0, 0);
var gone = Native.CreateWindowExW(0, "STATIC", "", 0, 0, 0, 24, 24, 0, 0, 0, 0);
if (window == 0 || gone == 0 || !Native.DestroyWindow(gone))
    throw new InvalidOperationException("Could not create the Windows reference windows.");
var handles = new Dictionary<string, nint> { ["window"] = window, ["null"] = 0, ["invalid"] = -99, ["destroyed"] = gone };
var events = new List<object>();
Native.EventHook observe = (_, eventCode, hwnd, objectId, childId, _, _) =>
{
    if (childId < 101 || childId > 106) return;
    foreach (var entry in handles)
    {
        if (entry.Value != hwnd) continue;
        events.Add(new { eventCode, window = entry.Key, objectId, childId });
        return;
    }
    events.Add(new { eventCode, window = "unrecognized", objectId, childId });
};
var hook = Native.SetWinEventHook(0x800c, 0x800e, 0, observe,
    (uint)Environment.ProcessId, Native.GetCurrentThreadId(), 0);
if (hook == 0) throw new InvalidOperationException("Could not install the Windows accessibility event hook.");
try
{
    var operations = new[]
    {
        new { eventCode = 0x800c, window = "window", objectId = -4, childId = 101 },
        new { eventCode = 0x800e, window = "window", objectId = 0, childId = 102 },
        new { eventCode = 0x800c, window = "null", objectId = -4, childId = 103 },
        new { eventCode = 0x800c, window = "invalid", objectId = -4, childId = 104 },
        new { eventCode = 0x800c, window = "destroyed", objectId = -4, childId = 105 },
        new { eventCode = 0x800c, window = "window", objectId = 37, childId = 106 }
    };
    foreach (var operation in operations)
        NotifyGuest.Notify(operation.eventCode, handles[operation.window], operation.objectId, operation.childId);
    var elapsed = Stopwatch.StartNew();
    while (elapsed.ElapsedMilliseconds < 250)
    {
        while (Native.PeekMessageW(out var message, 0, 0, 0, 1))
        {
            Native.TranslateMessage(in message);
            Native.DispatchMessageW(in message);
        }
        Thread.Sleep(1);
    }
    if (events.Count < 3) throw new InvalidOperationException("The registered hook missed valid-window notifications.");
    File.WriteAllText(args[0], JsonSerializer.Serialize(new
    {
        clr = Environment.Version.ToString(),
        assembly = typeof(NotifyGuest).Assembly.Location,
        operations,
        events
    }));
}
finally
{
    Native.UnhookWinEvent(hook);
    Native.DestroyWindow(window);
    GC.KeepAlive(observe);
}

static class Native
{
    internal delegate void EventHook(nint hook, uint eventCode, nint window, int objectId, int childId, uint threadId, uint time);

    [StructLayout(LayoutKind.Sequential)]
    internal struct Message
    {
        internal nint Window;
        internal uint Id;
        internal nuint WParam;
        internal nint LParam;
        internal uint Time;
        internal int X, Y;
        internal uint Private;
    }

    [DllImport("USER32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    internal static extern nint CreateWindowExW(uint extendedStyle, string className, string title, uint style,
        int x, int y, int width, int height, nint parent, nint menu, nint instance, nint parameter);

    [DllImport("USER32.dll", ExactSpelling = true)]
    internal static extern bool DestroyWindow(nint window);

    [DllImport("USER32.dll", ExactSpelling = true)]
    internal static extern nint SetWinEventHook(uint minimum, uint maximum, nint module, EventHook callback,
        uint processId, uint threadId, uint flags);

    [DllImport("USER32.dll", ExactSpelling = true)]
    internal static extern bool UnhookWinEvent(nint hook);

    [DllImport("USER32.dll", ExactSpelling = true)]
    internal static extern bool PeekMessageW(out Message message, nint window, uint minimum, uint maximum, uint flags);

    [DllImport("USER32.dll", ExactSpelling = true)]
    internal static extern bool TranslateMessage(in Message message);

    [DllImport("USER32.dll", ExactSpelling = true)]
    internal static extern nint DispatchMessageW(in Message message);

    [DllImport("KERNEL32.dll", ExactSpelling = true)]
    internal static extern uint GetCurrentThreadId();
}
