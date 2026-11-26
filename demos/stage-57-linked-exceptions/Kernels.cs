using System;
using ExceptionBridge;

namespace LinkedExceptions
{
    public static class Kernels
    {
        public static int Run(int value, int divisor, int cleanup)
        {
            int trace = 2;
            int result;
            try
            {
                try { result = Bridge.Work(value, divisor, cleanup); }
                finally { trace += 7; }
            }
            catch (OverflowException) { return -trace; }
            return result + trace;
        }

        public static int Propagate(int value, int divisor) => Bridge.Escape(value, divisor);
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
