using System;

namespace FinallyLibrary
{
    public static class Kernels
    {
        public static int NestedLeave(int route)
        {
            int trace = 1;
            try
            {
                try
                {
                    if (route == 0) goto Finished;
                    trace = 5;
                }
                finally { trace = trace * 10 + 2; }
                trace = trace * 10 + 3;
            }
            finally { trace = trace * 10 + 4; }
        Finished:
            return trace;
        }

        public static int CatchAfterCleanup(int divisor)
        {
            int trace = 1;
            try
            {
                try
                {
                    try { trace += Divide(20, divisor); }
                    finally { trace = trace * 10 + 2; }
                }
                finally { trace = trace * 10 + 3; }
            }
            catch (DivideByZeroException) { return trace * 10 + 4; }
            return trace;
        }

        public static int Replacement(int divisor, int value)
        {
            int trace = 1;
            try
            {
                try { trace = Divide(12, divisor); }
                finally { trace += checked(value + 1); }
            }
            catch (DivideByZeroException) { return trace + 100; }
            catch (OverflowException) { return trace + 200; }
            return trace;
        }

        public static int NestedInCleanup(int divisor, int cleanupDivisor)
        {
            int trace = 2;
            try
            {
                try { trace += Divide(20, divisor); }
                finally
                {
                    try { trace += Divide(6, cleanupDivisor); }
                    catch (DivideByZeroException) { trace += 13; }
                    finally { trace += 17; }
                }
            }
            catch (DivideByZeroException) { return trace + 100; }
            return trace;
        }

        public static int ReturnBeforeCleanup(int value)
        {
            try { return value; }
            finally { value = checked(value + 1); }
        }

        public static int Plain(int value) => value + 2;

        private static int Divide(int a, int b) => a / b;
    }

    public static class FinallyDemo
    {
        public static byte[] CreateBytes(int length) => new byte[length];
        public static void SetByte(byte[] data, int index, int value) => data[index] = unchecked((byte)value);
        public static int ReadByte(byte[] data, int index) => data[index];
        public static int FindRow(byte[] image, byte[] name) => PeLibrary.MethodNames.FindRow(image, name);
        public static int Compile(byte[] image, int row, byte[] output) =>
            PortableCompiler.MethodCompiler.Compile(image, row, output);
    }
}
