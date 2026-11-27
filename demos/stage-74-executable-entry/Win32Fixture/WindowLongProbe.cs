using System.Runtime.InteropServices;
using Windows.Win32.Foundation;

public static unsafe class WindowLongProbe
{
    [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)]
    private static extern int GetWindowLongW(HWND window, int index);

    [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)]
    private static extern nint GetWindowLongPtrW(HWND window, int index);

    public static int ReadWindowLong(nint handle, int index, int pointer)
    {
        var window = new HWND((void*)handle);
        nint result = pointer == 0 ? GetWindowLongW(window, index) : GetWindowLongPtrW(window, index);
        // Process-local handles cannot be compared numerically with Wasm handles.
        return index == -4 || index == -6 || index == -8 ? (result == 0 ? 0 : 1) : (int)result;
    }

    public static int ReadWindowLongError() => Marshal.GetLastPInvokeError();
}
