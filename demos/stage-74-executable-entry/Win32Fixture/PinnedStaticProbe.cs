public static unsafe class PinnedStaticProbe
{
    private struct Pair
    {
        internal int First;
        internal int Second;
    }

    private static Pair s_value = new Pair { First = 3, Second = 5 };
    private static Pair s_other;

    public static int Alias(int value)
    {
        int initial = ReadPair(s_value);
        fixed (Pair* pointer = &s_value, other = &s_other)
        {
            pointer->First = value;
            int direct = ReadPair(s_value);
            s_value = new Pair { First = 7, Second = 11 };
            other->Second = 13;
            int indirect = pointer->First * 100 + pointer->Second;
            return initial * 1000000 + direct * 10000 + indirect * 10 + ReadPair(s_other);
        }
    }

    private static int ReadPair(Pair pair) => pair.First + pair.Second;

    public static int Sum() => ReadPair(s_value) + ReadPair(s_other);
}
