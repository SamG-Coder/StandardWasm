namespace LongIntegers
{
    // Negative fixture. Checked 64-bit arithmetic and conversions must be diagnosed, never lowered to wrapping ops.
    public static class CheckedWide
    {
        public static long Increment(long value) => checked(value + 1);

        public static int Narrow(long value) => checked((int)value);
    }
}
