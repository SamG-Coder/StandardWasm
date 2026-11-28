using System.Runtime.InteropServices;

public static class Guest
{
    public static int Register(nint definition) => RegisterClassW(definition);
    public static int ClassInfo(nint module, nint name, nint output) => GetClassInfoW(module, name, output);
    public static nint Create(nint kind, nint module) =>
        CreateWindowExW(0, kind, 0, 0x80000000, 0, 0, 100, 80, 0, 0, module, 0);

    public static nint SetClass(nint window, int index, nint procedure) => SetClassLongW(window, index, procedure);
    public static nint SetClassPointer(nint window, int index, nint procedure) => SetClassLongPtrW(window, index, procedure);
    public static nint SetWindow(nint window, int index, nint procedure) => SetWindowLongW(window, index, procedure);
    public static nint GetWindow(nint window, int index) => GetWindowLongW(window, index);

    public static int SystemError() => Marshal.GetLastSystemError();
    public static int SavedError() => Marshal.GetLastPInvokeError();
    public static void SetErrors(int system, int saved)
    {
        Marshal.SetLastPInvokeError(saved);
        Marshal.SetLastSystemError(system);
    }

    [DllImport("USER32.dll", ExactSpelling = true)]
    private static extern ushort RegisterClassW(nint definition);
    [DllImport("USER32.dll", ExactSpelling = true)]
    private static extern int GetClassInfoW(nint module, nint name, nint output);
    [DllImport("USER32.dll", ExactSpelling = true)]
    private static extern nint CreateWindowExW(int extended, nint kind, nint name, uint style, int x, int y, int width, int height,
        nint parent, nint menu, nint module, nint value);
    // The upstream methods have SetLastError=false, preserving the caller's saved slot.
    [DllImport("user32.dll")]
    private static extern nint SetClassLongW(nint window, int index, nint procedure);
    [DllImport("user32.dll")]
    private static extern nint SetClassLongPtrW(nint window, int index, nint procedure);
    [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)]
    private static extern nint SetWindowLongW(nint window, int index, nint procedure);
    [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)]
    private static extern nint GetWindowLongW(nint window, int index);
}
