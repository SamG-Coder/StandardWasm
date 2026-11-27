using System;
using System.Runtime.InteropServices;

namespace WindowAccessibilityEvents
{
    public static class NotifyGuest
    {
        [DllImport("USER32.dll", ExactSpelling = true)]
        private static extern void NotifyWinEvent(uint eventCode, IntPtr window, int objectId, int childId);

        [DllImport("USER32.dll", EntryPoint = "NotifyWinEvent", ExactSpelling = true)]
        private static extern int WrongReturn(uint eventCode, IntPtr window, int objectId, int childId);

        public static void Notify(int eventCode, IntPtr window, int objectId, int childId) =>
            NotifyWinEvent((uint)eventCode, window, objectId, childId);

        public static int RejectWrongReturn(IntPtr window) => WrongReturn(0x800c, window, -4, 0);
    }
}
