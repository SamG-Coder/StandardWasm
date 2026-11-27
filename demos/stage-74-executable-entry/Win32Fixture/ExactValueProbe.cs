using System.Runtime.CompilerServices;

public static class ExactValueProbe
{
    private struct First { public int Value; }
    private struct Second { public int Value; }

    public static int ExactValueRead(int value) => unchecked(
        ReadExact(new First { Value = value }, -71) + ReadExact(new Second { Value = value }, 23));

    private static int ReadExact<T>(T input, int fallback) where T : struct
    {
        if (typeof(T) != typeof(First)) return fallback;
        First snapshot = Unsafe.As<T, First>(ref input);
        snapshot.Value = unchecked(snapshot.Value * 7 + 11);
        return snapshot.Value;
    }
}
