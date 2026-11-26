using System;

namespace NullThrow
{
    public static class Cases
    {
        public static int Escape(int value)
        {
            if (value == 0) throw null;
            return value;
        }

        public static int Cleanup(int value)
        {
            int cleanup = 0;
            try
            {
                try
                {
                    if (value == 0) throw null;
                    cleanup = value;
                }
                finally { cleanup += 13; }
            }
            catch (NullReferenceException) { return cleanup + 100; }
            return cleanup;
        }

        public static int FromCall(int value)
        {
            try { return Escape(value); }
            catch (Exception) { return -207; }
        }
    }

    public static class ThrowDemo
    {
        public static byte[] CreateBytes(int length) => new byte[length];
        public static void SetByte(byte[] data, int index, int value) => data[index] = unchecked((byte)value);
        public static int ReadByte(byte[] data, int index) => data[index];
        public static int FindRow(byte[] image, byte[] name) => PeLibrary.MethodNames.FindRow(image, name);
        public static int Compile(byte[] image, int row, byte[] output) =>
            PortableCompiler.MethodCompiler.Compile(image, row, output);
    }
}
