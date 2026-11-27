using System.Runtime.InteropServices;
using System.Text.Json;

if (!OperatingSystem.IsWindows() || IntPtr.Size != 4)
    throw new InvalidOperationException("Windows x86 is required.");

// verify.mjs replays the same operation list against the browser host. Here
// Windows owns real desktop windows. The browser host registers equivalent
// guest windows with the same client geometry and window-procedure results.
var script = JsonSerializer.Deserialize<Operation[]>(File.ReadAllText(args[0]),
    new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
var source = JsonSerializer.Deserialize<JsonElement[]>(File.ReadAllText(args[0]))!;
var log = new List<(string window, uint message, nint wParam, int lParam)>();
var windows = new Dictionary<string, nint>();
var eraseResults = new Dictionary<nint, int>();
bool recording = false;
WndProc procedure = (hwnd, message, wParam, lParam) =>
{
    string? name = windows.FirstOrDefault(entry => entry.Value == hwnd).Key;
    // WM_GETOBJECT is desktop accessibility traffic delivered during peeks, not paint state.
    if (recording && name != null && message != 0x003d) log.Add((name, message, wParam, (int)lParam));
    if (message == 0x0014 && eraseResults.TryGetValue(hwnd, out int erased)) return erased; // WM_ERASEBKGND
    return DefWindowProcW(hwnd, message, wParam, lParam);
};
var instance = GetModuleHandleW(0);
var className = Marshal.StringToHGlobalUni("Stage340Paint");
var windowClass = new WindowClass { procedure = Marshal.GetFunctionPointerForDelegate(procedure), instance = instance, name = className };
if (RegisterClassW(ref windowClass) == 0) throw new InvalidOperationException("RegisterClassW failed.");

const uint visiblePopup = 0x90000000, hiddenPopup = 0x80000000;
const uint toolNoActivate = 0x08000080; // WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW
windows["A"] = Open(visiblePopup, 40, 40);
windows["B"] = Open(visiblePopup, 300, 40);
windows["H"] = Open(hiddenPopup, 40, 200);
eraseResults[windows["A"]] = 0;
eraseResults[windows["B"]] = 1;
eraseResults[windows["H"]] = 0;
Settle();

var paints = new Dictionary<string, nint>();
var operations = new List<object>();
var retired = new HashSet<string>();
try
{
    foreach (var (operation, index) in script.Select((operation, index) => (operation, index)))
    {
        log.Clear();
        recording = true;
        nint context = 0;
        object expected = Run(operation, ref context);
        recording = false;
        // An HDC value is host-specific. Compare only its relation to BeginPaint's result.
        object[] messages = log.Select(entry => new
        {
            entry.window, entry.message, entry.lParam,
            context = entry.message != 0x0014 ? "none" : entry.wParam == 0 ? "zero" :
                entry.wParam == context ? "paint" : "other"
        }).ToArray();
        // Destruction callbacks belong to DestroyWindow, which the browser test models directly.
        if (operation.Kind == "destroy") messages = [];
        operations.Add(new { operation = source[index], expected, messages });
    }
    File.WriteAllText(args[1], JsonSerializer.Serialize(new { assembly = typeof(PaintGuest).Assembly.Location,
        clr = Environment.Version.ToString(), operations, fixedBuffers = FixedBuffers() }));
}
finally
{
    foreach (var buffer in paints.Values) Marshal.FreeHGlobal(buffer);
    foreach (var (name, hwnd) in windows)
        if (!retired.Contains(name)) DestroyWindow(hwnd);
}

static unsafe object FixedBuffers()
{
    FixedMix mix = default;
    mix.Words[1] = -1234567;
    return new
    {
        sizes = FixedBufferGuest.FixedSizes(),
        fieldOffsets = FixedBufferGuest.FixedFieldAccess((nint)(&mix)),
        element = FixedBufferGuest.FixedElementAccess((nint)(&mix)),
        tag = mix.Tag, afterText = mix.AfterText, afterWide = mix.AfterWide, last = mix.Last
    };
}

nint Handle(string name) => name switch { "zero" => 0, "thread" => -1, "invalid" => -99, _ => windows[name] };

unsafe object Run(Operation operation, ref nint context)
{
    int* rectangle = stackalloc int[4];
    for (int i = 0; i < 4; i++) rectangle[i] = operation.Rect?[i] ?? -7;
    nint rectanglePointer = operation.Rect != null || operation.Output ? (nint)rectangle : 0;
    PaintGuest.SetErrors(789);
    switch (operation.Kind)
    {
        case "invalidate":
        {
            int result = PaintGuest.Invalidate(Handle(operation.Target!), rectanglePointer, operation.Erase);
            return new { result, error = PaintGuest.SystemError() };
        }
        case "validate":
        {
            int result = PaintGuest.Validate(Handle(operation.Target!), rectanglePointer);
            return new { result, error = PaintGuest.SystemError() };
        }
        case "update":
        {
            int result = PaintGuest.UpdateRect(Handle(operation.Target!), rectanglePointer, operation.Erase);
            int error = PaintGuest.SystemError();
            int[]? rect = operation.Output ? new[] { rectangle[0], rectangle[1], rectangle[2], rectangle[3] } : null;
            return new { result, error, rect };
        }
        case "erase-result":
            eraseResults[Handle(operation.Target!)] = (int)operation.Payload;
            return new { result = 1 };
        case "post-thread":
            return new { result = Guest.Post(Guest.ThreadId(), 0x8051, operation.Payload, -100, 1) };
        case "peek":
        {
            int* words = stackalloc int[8];
            for (int i = 0; i < 8; i++) words[i] = -1;
            int result = Guest.Peek((nint)words, Handle(operation.Target!), operation.Minimum, operation.Maximum, operation.Flags);
            int hwnd = words[0];
            string target = hwnd == -1 ? "unchanged" : hwnd == 0 ? "zero" : windows.Single(entry => entry.Value == hwnd).Key;
            return new { result, target, msg = (uint)words[1], wParam = words[2], lParam = words[3], guard = words[7] };
        }
        case "begin":
        {
            if (!paints.TryGetValue(operation.Buffer!, out nint buffer)) paints[operation.Buffer!] = buffer = Marshal.AllocHGlobal(64);
            byte* bytes = (byte*)buffer;
            for (int i = 0; i < 64; i++) bytes[i] = 0xcd;
            context = PaintGuest.Begin(Handle(operation.Target!), buffer);
            int error = PaintGuest.SystemError();
            int* words = (int*)buffer;
            string reserved = Convert.ToHexString(new ReadOnlySpan<byte>(bytes + 32, 32));
            return new { live = context != 0, stored = words[0] == context, fErase = words[1],
                rcPaint = new[] { words[2], words[3], words[4], words[5] }, fRestore = words[6], fIncUpdate = words[7],
                reserved, error };
        }
        case "end":
        {
            int result = PaintGuest.End(Handle(operation.Target!), paints[operation.Buffer!]);
            return new { result, error = PaintGuest.SystemError() };
        }
        case "destroy":
        {
            int result = DestroyWindow(Handle(operation.Target!));
            retired.Add(operation.Target!);
            return new { result };
        }
    }
    throw new InvalidOperationException("Unknown operation " + operation.Kind);
}

nint Open(uint style, int x, int y)
{
    nint hwnd = CreateWindowExW(toolNoActivate, className, 0, style, x, y, 200, 100, 0, 0, instance, 0);
    if (hwnd == 0) throw new InvalidOperationException("CreateWindowExW failed.");
    return hwnd;
}

unsafe void Settle()
{
    // Drain creation-time posts and validate creation-time paint so every
    // operation starts from clean update regions on both hosts.
    int* message = stackalloc int[7];
    for (int round = 0; round < 8; round++)
    {
        foreach (var hwnd in windows.Values) ValidateRectRaw(hwnd, 0);
        while (PeekMessageW((nint)message, 0, 0, 0, 1) != 0) DispatchMessageW((nint)message);
    }
    foreach (var hwnd in windows.Values) ValidateRectRaw(hwnd, 0);
}

delegate nint WndProc(nint hwnd, uint message, nint wParam, nint lParam);

[StructLayout(LayoutKind.Sequential)]
struct WindowClass { public uint style; public nint procedure; public int classExtra, windowExtra; public nint instance, icon, cursor, brush, menu, name; }

sealed class Operation
{
    public string Kind { get; set; } = "";
    public string? Target { get; set; }
    public int[]? Rect { get; set; }
    public bool Output { get; set; }
    public int Erase { get; set; }
    public uint Payload { get; set; }
    public uint Minimum { get; set; }
    public uint Maximum { get; set; }
    public uint Flags { get; set; }
    public string? Buffer { get; set; }
}

partial class Program
{
    [DllImport("USER32.dll", ExactSpelling = true)] static extern ushort RegisterClassW(ref WindowClass windowClass);
    [DllImport("USER32.dll", ExactSpelling = true)] static extern nint CreateWindowExW(uint extended, nint type, nint title, uint style,
        int x, int y, int width, int height, nint parent, nint menu, nint module, nint param);
    [DllImport("USER32.dll", ExactSpelling = true)] static extern nint DefWindowProcW(nint hwnd, uint message, nint wParam, nint lParam);
    [DllImport("USER32.dll", ExactSpelling = true)] static extern int DestroyWindow(nint hwnd);
    [DllImport("USER32.dll", ExactSpelling = true)] static extern int PeekMessageW(nint message, nint hwnd, uint minimum, uint maximum, uint flags);
    [DllImport("USER32.dll", ExactSpelling = true)] static extern nint DispatchMessageW(nint message);
    [DllImport("USER32.dll", ExactSpelling = true, EntryPoint = "ValidateRect")] static extern int ValidateRectRaw(nint hwnd, nint rectangle);
    [DllImport("KERNEL32.dll", ExactSpelling = true)] static extern nint GetModuleHandleW(nint name);
}
