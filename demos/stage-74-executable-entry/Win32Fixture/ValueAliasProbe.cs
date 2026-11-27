public readonly struct StoredValue
{
    public readonly int Code;
    public StoredValue(int code) { Code = code; }
}

public static class ValueAliasProbe
{
    public static int CheckValueAlias(int mode)
    {
        StoredValue first = new StoredValue(6);
        StoredValue second = new StoredValue(3);
        ref StoredValue selected = ref (mode == 0 ? ref first : ref second);
        CopyValue(ref selected, out StoredValue prior);
        if (mode == 2) ResetValue(out selected);
        else SetValue(out selected, new StoredValue(9));
        return prior.Code * 100 + first.Code * 10 + second.Code;
    }

    private static void CopyValue<T>(ref T source, out T destination) => destination = source;
    private static void ResetValue<T>(out T value) => value = default;
    private static void SetValue<T>(out T value, T next) => value = next;
}
