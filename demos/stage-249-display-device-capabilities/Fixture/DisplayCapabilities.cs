using System;
using System.Runtime.InteropServices;

namespace DisplayCapabilities
{
    public static class Probe
    {
        [DllImport("USER32.dll", ExactSpelling = true)]
        private static extern IntPtr GetDC(IntPtr window);

        [DllImport("USER32.dll", ExactSpelling = true)]
        private static extern int ReleaseDC(IntPtr window, IntPtr context);

        [DllImport("GDI32.dll", ExactSpelling = true)]
        private static extern int GetDeviceCaps(IntPtr context, int selector);

        public static IntPtr GetDesktop() => GetDC((nint)0);
        public static int Capability(IntPtr context, int selector) => GetDeviceCaps(context, selector);
        public static int ReleaseDesktop(IntPtr context) => ReleaseDC((nint)0, context);
    }
}
