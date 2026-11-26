using System;
using ForeignCounter;

namespace LinkedInstances
{
    public static class Kernels
    {
        public static int Run(int value, int delta)
        {
            var signed = new Counter(value);
            var unsigned = new Counter(unchecked((uint)value));
            Counter shifted = signed.Shift(delta);
            return unchecked(signed.Read() + unsigned.Shift((uint)delta) + (shifted == signed ? 1000 : 0));
        }

        public static int ReadNullable(Counter counter) => counter.Read();

        public static int EvaluateOnNull(Counter counter, int value)
        {
            try { return counter.Shift((uint)checked(value + 1)); }
            catch (OverflowException) { return 1; }
            catch (NullReferenceException) { return 2; }
        }

        public static int ConstructWide(long value)
        {
            try { return new Counter(value).Read(); }
            catch (OverflowException) { return 47; }
        }

        public static int VirtualRead() => ((DynamicCounter)null).Read();
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
