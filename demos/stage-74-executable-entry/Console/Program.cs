internal static class Program
{
    private static int Main() => Fold(11) + 4;

    private static int Fold(int value)
    {
        int sum = 0;
        for (int i = 0; i < value; i++)
            if ((i & 1) == 0) sum += i;
        return sum + 3;
    }
}
