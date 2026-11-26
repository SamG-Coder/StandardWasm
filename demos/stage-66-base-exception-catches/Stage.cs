using System;

namespace BaseExceptionHandlers
{
    public static class Handlers
    {
        public static int Ordered(int numerator, int denominator)
        {
            try { return numerator / denominator; }
            catch (DivideByZeroException) { return -101; }
            catch (Exception) { return -102; }
        }

        public static int ArrayAccess(int length, int index)
        {
            try
            {
                int[] data = length == 0 ? null : new int[length];
                return data[index];
            }
            catch (NullReferenceException) { return -201; }
            catch (SystemException) { return -202; }
        }

        public static int Rethrow(int numerator, int denominator)
        {
            int cleanup = 0;
            try
            {
                try { return numerator / denominator; }
                catch (Exception) { throw; }
                finally { cleanup = 17; }
            }
            catch (DivideByZeroException) { return cleanup + 101; }
            catch (OverflowException) { return cleanup + 102; }
        }
    }
    public static class BaseExceptionDemo
    {
        public static byte[] CreateBytes(int length) => new byte[length];
        public static void SetByte(byte[] data, int index, int value) => data[index] = unchecked((byte)value);
        public static int ReadByte(byte[] data, int index) => data[index];
        public static int FindRow(byte[] image, byte[] name) => PeLibrary.MethodNames.FindRow(image, name);
        public static int Compile(byte[] image, int row, byte[] output) =>
            PortableCompiler.MethodCompiler.Compile(image, row, output);
    }
}
