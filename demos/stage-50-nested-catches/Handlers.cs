using System;

namespace NestedHandlers
{
    public static class Handlers
    {
        public static int InnerThenOuter(int a, int b)
        {
            try
            {
                try { return Divide(a, b); }
                catch (DivideByZeroException) { return -101; }
            }
            catch (ArithmeticException) { return -102; }
        }

        public static int HandlerEscapesToOuter(int a, int b)
        {
            try
            {
                int result;
                try { result = Divide(a, b); }
                catch (DivideByZeroException) { result = checked(a + 1); }
                return result ^ 17; // Both inner exits resume inside the outer try.
            }
            catch (OverflowException) { return -201; }
        }

        public static int NestedInsideHandler(int a, int b)
        {
            try { return checked(a + 1); }
            catch (OverflowException)
            {
                int result;
                try { result = Divide(9, b); }
                catch (DivideByZeroException) { result = -301; }
                return result + 5; // The inner leave stays inside the outer catch.
            }
        }

        public static int Disjoint(int a, int b)
        {
            int result;
            try { result = checked(a + 1); }
            catch (OverflowException) { result = -401; }
            try { return Divide(result, b); }
            catch (DivideByZeroException) { return -402; }
        }

        public static int Escapes(int a, int b)
        {
            try
            {
                try { return Divide(a, b); }
                catch (OverflowException) { return -501; }
            }
            catch (OverflowException) { return -502; }
        }

        // More than 32 IL states, so the dispatch loop groups them into blocks (decision 0102).
        // The loop's back edge and both handlers' resumptions branch across those groups.
        public static int GroupedLoop(int a, int b)
        {
            int total = 0;
            for (int i = 0; i < 12; i++)
            {
                try
                {
                    total += Divide(a + i, b - i % 3);
                    total = checked(total + a);
                }
                catch (DivideByZeroException) { total -= 7 * i; }
                catch (OverflowException) { total ^= i; }
                total = (total << 1) ^ ((total >> 3) + i);
            }
            return total;
        }

        private static int Divide(int a, int b) => a / b;
    }

    public static class NestedDemo
    {
        public static byte[] CreateBytes(int length) => new byte[length];
        public static void SetByte(byte[] data, int index, int value) => data[index] = unchecked((byte)value);
        public static int ReadByte(byte[] data, int index) => data[index];
        public static int FindRow(byte[] image, byte[] name) => PeLibrary.MethodNames.FindRow(image, name);
        public static int Compile(byte[] image, int row, byte[] output) =>
            PortableCompiler.MethodCompiler.Compile(image, row, output);
    }
}
