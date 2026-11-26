using System;
using ObjectBridge;

namespace LinkedObjects
{
    public sealed class EntryCell
    {
        public int Value;
        public EntryCell Next;
    }

    public static class Kernels
    {
        public static int Run(int value)
        {
            var state = new EntryCell { Value = unchecked(value + 2), Next = new EntryCell { Value = 3 } };
            return unchecked(Bridge.Compute(value) + state.Value + state.Next.Value);
        }

        public static int Identity(int foreign)
        {
            var local = new EntryCell();
            object before = Pick(foreign, local);
            object after = Bridge.Echo(before);
            return (after == before ? 1 : 0) + (after == (object)local ? 2 : 0);
        }

        private static object Pick(int foreign, object local)
        {
            if (foreign == 0) return local;
            return Bridge.Make(foreign);
        }

        public static int Touch(int present) => Bridge.Touch(present);

        public static int CatchTouch(int present)
        {
            int trace = 1;
            try
            {
                try { trace += Bridge.Touch(present); }
                finally { trace *= 3; }
            }
            catch (NullReferenceException) { return -trace; }
            return trace;
        }
    }

    public static class LinkDemo
    {
        public static byte[] CreateBytes(int length) => new byte[length];
        public static void SetByte(byte[] data, int index, int value) => data[index] = unchecked((byte)value);
        public static int ReadByte(byte[] data, int index) => data[index];
        public static int FindRow(byte[] image, byte[] name) => PeLibrary.MethodNames.FindRow(image, name);
        public static int CompileBundle(byte[] bundle, byte[] entries, byte[] output) =>
            PortableCompiler.MethodCompiler.CompileBundle(bundle, entries, output);
    }
}
