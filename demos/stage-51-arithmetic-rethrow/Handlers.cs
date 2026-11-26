using System;

namespace RethrowHandlers
{
    public static class Handlers
    {
        public static int Escape(int a, int b)
        {
            try { return Divide(a, b); }
            catch (ArithmeticException) { throw; }
        }

        public static int RetainOuter(int a, int b)
        {
            try
            {
                try { return Divide(a, b); }
                catch (DivideByZeroException)
                {
                    int flag;
                    try { flag = checked(a + 1); }
                    catch (OverflowException) { flag = 0; }
                    if (flag == 0) throw;
                    return flag;
                }
            }
            catch (DivideByZeroException) { return -101; }
            catch (OverflowException) { return -102; }
        }

        public static int RethrowInner(int a, int b)
        {
            try { return Divide(a, b); }
            catch (DivideByZeroException)
            {
                try { return checked(a + 1); }
                catch (OverflowException) { throw; }
            }
        }

        public static int RethrowInsideTry(int a, int b)
        {
            try { return Divide(a, b); }
            catch (DivideByZeroException)
            {
                try { throw; }
                catch (DivideByZeroException) { return -301; }
            }
        }

        public static int ViaUnmatchedFrame(int a, int b)
        {
            try { return Escape(a, b); }
            catch (OverflowException) { return -401; }
        }

        public static int Plain(int a, int b) => a + b;

        private static int Divide(int a, int b) => a / b;
    }

    public static class RethrowDemo
    {
        public static byte[] CreateBytes(int length) => new byte[length];
        public static void SetByte(byte[] data, int index, int value) => data[index] = unchecked((byte)value);
        public static int ReadByte(byte[] data, int index) => data[index];
        public static int FindRow(byte[] image, byte[] name) => PeLibrary.MethodNames.FindRow(image, name);
        public static int Compile(byte[] image, int row, byte[] output) =>
            PortableCompiler.MethodCompiler.Compile(image, row, output);
    }
}
