using System;

namespace OverflowHandlers
{
    public static class Handlers
    {
        public static int Increment(int value)
        {
            try { return checked(value + 1); }
            catch (OverflowException) { return -1; }
        }

        public static int FromCall(int value, int offset)
        {
            try
            {
                if (value == 0) return offset;
                return checked(Double(value) + offset);
            }
            catch (OverflowException) { return offset - 1; }
        }

        private static int Double(int value) => checked(value * 2);

        public static long DivideOrRemainder(int operation, long a, long b)
        {
            try { return operation == 0 ? a / b : a % b; }
            catch (OverflowException) { return 909; }
        }

        public static int BeforeTry(int outside, int inside)
        {
            int prefix = checked(outside + 1);
            try { return checked(inside + prefix); }
            catch (OverflowException) { return prefix; }
        }

        public static int InsideHandler(int value, int fallback)
        {
            try { return checked(value + 1); }
            catch (OverflowException) { return checked(fallback + 1); }
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
