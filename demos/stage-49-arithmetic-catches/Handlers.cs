using System;

namespace ArithmeticHandlers
{
    public static class Handlers
    {
        public static int SpecificThenBase(int a, int b)
        {
            try { return Divide(a, b); }
            catch (DivideByZeroException) { return -101; }
            catch (ArithmeticException) { return -102; }
        }

        public static int ExactSiblings(int a, int b)
        {
            try { return a % b; }
            catch (OverflowException) { return -201; }
            catch (DivideByZeroException) { return -202; }
        }

        public static int BaseCatch(int a, int b)
        {
            try { return Divide(a, b); }
            catch (ArithmeticException) { return -301; }
        }

        public static int ExactOnly(int a, int b)
        {
            try { return Divide(a, b); }
            catch (DivideByZeroException) { return -401; }
        }

        public static uint UnsignedDivide(uint a, uint b)
        {
            try { return a / b; }
            catch (DivideByZeroException) { return 501; }
        }

        public static int SiblingDoesNotCatchHandler(int a, int b)
        {
            try { return Divide(a, b); }
            catch (DivideByZeroException) { return checked(a + 1); }
            catch (ArithmeticException) { return -601; }
        }

        public static int ArrayAccess(int index)
        {
            try
            {
                var data = new int[1];
                return data[index];
            }
            catch (ArithmeticException) { return -701; }
        }

        public static int Nested(int a, int b)
        {
            try
            {
                try { return Divide(a, b); }
                catch (DivideByZeroException) { return -801; }
            }
            catch (OverflowException) { return -802; }
        }

        private static int Divide(int a, int b) => a / b;
    }

    public static class ArithmeticDemo
    {
        public static byte[] CreateBytes(int length) => new byte[length];
        public static void SetByte(byte[] data, int index, int value) => data[index] = unchecked((byte)value);
        public static int ReadByte(byte[] data, int index) => data[index];
        public static int FindRow(byte[] image, byte[] name) => PeLibrary.MethodNames.FindRow(image, name);
        public static int Compile(byte[] image, int row, byte[] output) =>
            PortableCompiler.MethodCompiler.Compile(image, row, output);
    }
}
