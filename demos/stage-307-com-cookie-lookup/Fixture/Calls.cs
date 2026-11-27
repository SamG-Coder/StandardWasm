using System;
using System.Runtime.InteropServices;

namespace ComCookieFixture
{
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct InterfaceTable
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
        public static int Lookup(nint table, uint cookie, nint iid, nint output)
        {
            InterfaceTable* receiver = (InterfaceTable*)table;
            var lookup = (delegate* unmanaged[Stdcall]<InterfaceTable*, uint, Guid*, void**, int>)receiver->VTable[5];
            return lookup(receiver, cookie, (Guid*)iid, (void**)output);
        }

        public static int LookupWrapped(nint table, uint cookie, nint iid, nint output)
        {
            InterfaceTable* receiver = (InterfaceTable*)table;
            var lookup = (delegate* unmanaged[Stdcall]<InterfaceTable*, uint, Guid*, void**, Result>)receiver->VTable[5];
            return lookup(receiver, cookie, (Guid*)iid, (void**)output).Code;
        }

        public static int WrongOutput(nint table, uint cookie, nint iid, nint output)
        {
            InterfaceTable* receiver = (InterfaceTable*)table;
            var lookup = (delegate* unmanaged[Stdcall]<InterfaceTable*, uint, Guid*, uint*, Result>)receiver->VTable[5];
            return lookup(receiver, cookie, (Guid*)iid, (uint*)output).Code;
        }
    }
}
