using System.Runtime.InteropServices;

namespace ComCallFixture
{
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct Site
    {
        public void** VTable;
    }

    [StructLayout(LayoutKind.Sequential)]
    public readonly struct Result
    {
        public readonly int Code;
        public Result(int code) => Code = code;
    }

    public static unsafe class Calls
    {
        public static int Scalar(nint receiver)
        {
            Site* site = (Site*)receiver;
            var notify = (delegate* unmanaged[Stdcall]<Site*, int>)site->VTable[3];
            return notify(site);
        }

        public static int Wrapped(nint receiver)
        {
            Site* site = (Site*)receiver;
            var notify = (delegate* unmanaged[Stdcall]<Site*, Result>)site->VTable[3];
            return notify(site).Code;
        }

        public static int Clear(nint receiver)
        {
            *Slot(receiver) = null;
            return *Slot(receiver) == null ? 1 : 0;
        }

        private static void** Slot(nint receiver) => ((Site*)receiver)->VTable + 3;
    }
}
