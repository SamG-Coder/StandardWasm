namespace FloatingPoint
{
    // A checked float-to-int conversion raises managed overflow when the result cannot fit.
    public static class CheckedConversions
    {
        public static int Narrow(double value) => checked((int)value);
    }
}
