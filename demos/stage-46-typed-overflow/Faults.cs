using System;

namespace TypedOverflow
{
    public static class Faults
    {
        public static int AddOrDivide(int operation, int a, int b) =>
            operation == 0 ? checked(a + b) : a / b;

        public static long Product(long a, long b) => checked(a * b);

        public static uint Carry(uint a, uint b) => checked(a + b);

        public static int ToByte(double value) => checked((byte)value);

        public static int RemainderWithGlobal(double a, double b)
        {
            if (EventArgs.Empty != SharedEventArgs()) return -1;
            return checked((int)(a % b));
        }

        private static EventArgs SharedEventArgs() => EventArgs.Empty;
    }

    public static class OverflowDemo
    {
        public static byte[] CreateBytes(int length) => new byte[length];
        public static void SetByte(byte[] data, int index, int value) => data[index] = unchecked((byte)value);
        public static int ReadByte(byte[] data, int index) => data[index];
        public static int FindRow(byte[] image, byte[] name) => PeLibrary.MethodNames.FindRow(image, name);
        public static int Compile(byte[] image, int row, byte[] output) =>
            PortableCompiler.MethodCompiler.Compile(image, row, output);
    }
}
