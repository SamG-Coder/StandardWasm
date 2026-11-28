using System;

namespace ReferencedDelegateDependency
{
    public delegate long Combine(long a, int b);
    public delegate void Sink(string text);
    public delegate Counter Factory(int start);

    public sealed class Counter
    {
        public int Value;
        public int Next() => ++Value;
    }

    // Win32-style wrapper structs, as in Windows.Win32.Foundation.HWND and BOOL.
    public struct Handle { public IntPtr Value; }
    public struct Flag { public int Value; }

    public static class Native
    {
        // A nested delegate over wrapper structs, shaped like WinForms'
        // PInvokeCore.EnumThreadWindowsCallback(HWND) -> BOOL, but enumerated in managed code.
        public delegate Flag EnumCallback(Handle window, IntPtr state);

        public static int Enumerate(EnumCallback callback, IntPtr state)
        {
            int visited = 0;
            for (int window = 1; window <= 5; window++)
            {
                visited++;
                if (callback(new Handle { Value = (IntPtr)window }, state).Value == 0) break;
            }
            return visited;
        }
    }

    // The dependency invokes delegates its callers construct.
    public static class Library
    {
        public static long Apply(Combine combine, long a, int b) => combine(a, b);
        public static void Send(Sink sink, string text) => sink(text);
        public static Combine Doubler() => (a, b) => a * 2 + b;
        public static long Mix(long a, int b) => a * 100 + b;
    }

    public class Shape
    {
        public virtual int Sides(int scale) => 0 * scale;
    }
}
