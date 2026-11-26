using System;

namespace ExceptionLeaf
{
    public static class Leaf
    {
        public static int DivideAndRethrow(int value, int divisor)
        {
            try { return value / divisor; }
            catch (ArithmeticException) { throw; }
        }
    }
}
