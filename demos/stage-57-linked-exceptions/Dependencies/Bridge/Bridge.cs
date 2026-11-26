using System;
using ExceptionLeaf;

namespace ExceptionBridge
{
    public static class Bridge
    {
        public static int Work(int value, int divisor, int cleanup)
        {
            int trace = 3;
            try
            {
                try { trace = checked(trace + Leaf.DivideAndRethrow(value, divisor)); }
                finally { trace = checked(trace + cleanup); }
            }
            catch (DivideByZeroException) { return trace + 100; }
            return trace;
        }

        public static int Escape(int value, int divisor)
        {
            try { return Leaf.DivideAndRethrow(value, divisor); }
            catch (OverflowException) { throw; }
        }
    }
}
