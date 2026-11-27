using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Windows.Win32.Foundation;

public static unsafe class Guest
{
    private static nint first, second, third, lastPayload;
    private static int count, limit;

    public static int Query(uint thread, nint payload, int stopAfter)
    {
        count = 0;
        limit = stopAfter;
        lastPayload = 0;
        return EnumThreadWindows(thread, &Visit, new LPARAM(payload)).Value != 0 ? 1 : 0;
    }

    public static int Count() => count;
    public static nint Payload() => lastPayload;
    public static nint HandleAt(int index) => index == 0 ? first : index == 1 ? second : third;

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static BOOL Visit(HWND window, LPARAM payload)
    {
        if (count == 0) first = window.Value;
        else if (count == 1) second = window.Value;
        else third = window.Value;
        count++;
        lastPayload = payload.Value;
        return new BOOL(count < limit ? 1 : 0);
    }

    // The CLR fail-fasts when an exception escapes an UnmanagedCallersOnly method, so this
    // never returns there. Only the Wasm side runs it; the oracle process cannot survive it.
    public static int EscapeCallback(uint thread) => EnumThreadWindows(thread, &Escape, default).Value;

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static BOOL Escape(HWND window, LPARAM payload) => new BOOL(checked(int.MaxValue + (int)window.Value)); // OverflowException

    public static int RejectCallingConvention(uint thread) => WrongCallingConvention(thread, null, default).Value;

    [DllImport("USER32.dll", ExactSpelling = true)]
    private static extern BOOL EnumThreadWindows(uint thread, delegate* unmanaged[Stdcall]<HWND, LPARAM, BOOL> visit, LPARAM payload);

    [DllImport("USER32.dll", EntryPoint = "EnumThreadWindows", ExactSpelling = true)]
    private static extern BOOL WrongCallingConvention(uint thread, delegate* unmanaged[Cdecl]<HWND, LPARAM, BOOL> visit, LPARAM payload);
}

namespace Windows.Win32.Foundation
{
    [StructLayout(LayoutKind.Sequential)]
    internal readonly struct HWND
    {
        internal readonly nint Value;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal readonly struct LPARAM
    {
        internal readonly nint Value;
        internal LPARAM(nint value) => Value = value;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal readonly struct BOOL
    {
        internal readonly int Value;
        internal BOOL(int value) => Value = value;
    }
}
