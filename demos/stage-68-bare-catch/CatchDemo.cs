using System;

namespace BareCatch
{
    public static class Handlers
    {
        public static int Ordered(int left, int right)
        {
            try { return left / right; }
            catch (DivideByZeroException) { return -101; }
            catch { return -102; }
        }

        public static int Fallback(int value, int divisor)
        {
            try
            {
                if (value == 0) throw null;
                return value / divisor;
            }
            catch { return -201; }
        }

        public static int Rethrow(int left, int right)
        {
            try
            {
                try { return left / right; }
                catch { throw; }
            }
            catch (DivideByZeroException) { return -301; }
            catch (OverflowException) { return -302; }
        }
    }

    public static class CatchDemo
    {
        public static byte[] CreateBytes(int length) => new byte[length];
        public static void SetByte(byte[] data, int index, int value) => data[index] = unchecked((byte)value);
        public static int ReadByte(byte[] data, int index) => data[index];
        public static int FindRow(byte[] image, byte[] name) => PeLibrary.MethodNames.FindRow(image, name);
        public static int Compile(byte[] image, int row, byte[] output) =>
            PortableCompiler.MethodCompiler.Compile(image, row, output);
    }
}
