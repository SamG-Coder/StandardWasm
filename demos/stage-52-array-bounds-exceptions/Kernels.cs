using System;

namespace ArrayBounds
{
    public static class Kernels
    {
        public static int Read(int length, int index)
        {
            var data = new int[length];
            return data[index];
        }

        public static int CatchRead(int length, int index)
        {
            try { return Read(length, index); }
            catch (IndexOutOfRangeException) { return -101; }
        }

        public static int StoreByte(int index, int value)
        {
            var data = new byte[1];
            data[0] = 13;
            try { data[index] = unchecked((byte)value); }
            catch (IndexOutOfRangeException) { return -data[0]; }
            return data[0];
        }

        public static int StoreWide(int index)
        {
            var data = new long[1];
            data[index] = 1L << 50;
            return data[0] == 1L << 50 ? 1 : 0;
        }

        public static int Rethrow(int mode, int index)
        {
            try
            {
                try { return mode == 0 ? Read(1, index) : checked(index + 1); }
                catch (IndexOutOfRangeException) { throw; }
            }
            catch (ArithmeticException) { return -201; }
            catch (IndexOutOfRangeException) { return -202; }
        }

        public static int NullArray(int[] data, int index)
        {
            try { return data[index]; }
            catch (IndexOutOfRangeException) { return -301; }
        }
    }

    public static class BoundsDemo
    {
        public static byte[] CreateBytes(int length) => new byte[length];
        public static void SetByte(byte[] data, int index, int value) => data[index] = unchecked((byte)value);
        public static int ReadByte(byte[] data, int index) => data[index];
        public static int FindRow(byte[] image, byte[] name) => PeLibrary.MethodNames.FindRow(image, name);
        public static int Compile(byte[] image, int row, byte[] output) =>
            PortableCompiler.MethodCompiler.Compile(image, row, output);
    }
}
