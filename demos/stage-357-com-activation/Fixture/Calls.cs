using System;
using System.Runtime.InteropServices;

namespace ComActivationFixture
{
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct InterfaceTable
    {
        public void** VTable;
    }

    public static unsafe class Calls
    {
        public static int Activate(nint clsid, nint outer, uint context, nint iid, nint output) =>
            CoCreateInstance((Guid*)clsid, (void*)outer, context, (Guid*)iid, (void**)output);

        public static int Lookup(nint table, uint cookie, nint iid, nint output)
        {
            InterfaceTable* receiver = (InterfaceTable*)table;
            var lookup = (delegate* unmanaged[Stdcall]<InterfaceTable*, uint, Guid*, void**, int>)receiver->VTable[5];
            return lookup(receiver, cookie, (Guid*)iid, (void**)output);
        }

        public static int WrongOutput(nint clsid, nint outer, uint context, nint iid, nint output) =>
            BadOutput((Guid*)clsid, (void*)outer, context, (Guid*)iid, (nint*)output);

        public static int WrongLibrary(nint clsid, nint outer, uint context, nint iid, nint output) =>
            BadLibrary((Guid*)clsid, (void*)outer, context, (Guid*)iid, (void**)output);

        [DllImport("OLE32.dll", ExactSpelling = true)]
        private static extern int CoCreateInstance(Guid* clsid, void* outer, uint context, Guid* iid, void** output);

        [DllImport("OLE32.dll", EntryPoint = "CoCreateInstance", ExactSpelling = true)]
        private static extern int BadOutput(Guid* clsid, void* outer, uint context, Guid* iid, nint* output);

        [DllImport("USER32.dll", EntryPoint = "CoCreateInstance", ExactSpelling = true)]
        private static extern int BadLibrary(Guid* clsid, void* outer, uint context, Guid* iid, void** output);
    }
}
