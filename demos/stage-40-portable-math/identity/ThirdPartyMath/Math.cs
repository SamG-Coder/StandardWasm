namespace System
{
    // Not the BCL: Sqrt deliberately returns value + 2. A call to it must never be lowered to f64.sqrt.
    public static class Math
    {
        public static double Sqrt(double value) => value + 2;
    }
}
