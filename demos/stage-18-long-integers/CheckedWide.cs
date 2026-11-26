namespace LongIntegers
{
    // Checked 64-bit arithmetic and conversions raise managed overflow outside the result range.
    public static class CheckedWide
    {
        public static long Increment(long value) => checked(value + 1);

        public static int Narrow(long value) => checked((int)value);
    }
}
