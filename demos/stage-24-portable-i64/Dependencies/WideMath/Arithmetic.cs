namespace WideMath
{
    public static class Arithmetic
    {
        public static long Scale(long value, int scale) => unchecked(value * scale + 0x123456789abcdefL);
        public static ulong Scale(ulong value, int shift) => value >> shift;
    }
}
