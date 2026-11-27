using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows.Forms;

internal static unsafe class WindowProcedureOracle
{
    private static nint expectedWindow;
    private static int[] observed;

    internal static object Observe()
    {
        var window = new NativeWindow();
        window.CreateHandle(new CreateParams { Caption = "StandardWasm procedure probe", Parent = (nint)(-3) });
        try
        {
            expectedWindow = window.Handle;
            delegate* unmanaged[Stdcall]<nint, uint, nuint, nint, nint> first = &First, second = &Second;
            nint before = CallWindowProcW((nint)first, expectedWindow, 0x80000111, 0xfedcba98, unchecked((int)0x87654321));
            int[] firstArgs = observed;
            nint after = CallWindowProcW((nint)second, expectedWindow, 0x7fff, 0x80000000, -1);
            return new { received = new[] { firstArgs, observed }, results = new[] { (int)before, (int)after } };
        }
        finally { window.DestroyHandle(); }
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static nint First(nint window, uint message, nuint wparam, nint lparam)
    {
        observed = new[] { 1, window == expectedWindow ? 1 : 0, unchecked((int)message), unchecked((int)wparam), (int)lparam };
        return -315851420;
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static nint Second(nint window, uint message, nuint wparam, nint lparam)
    {
        observed = new[] { 2, window == expectedWindow ? 1 : 0, unchecked((int)message), unchecked((int)wparam), (int)lparam };
        return 1234567;
    }

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern nint CallWindowProcW(nint procedure, nint window, uint message, nuint wparam, nint lparam);
}
