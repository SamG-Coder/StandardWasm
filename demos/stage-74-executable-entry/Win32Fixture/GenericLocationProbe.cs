public static class GenericLocationProbe
{
    public static int GenericBooleanLocations(int scenario)
    {
        bool value = scenario != 0;
        bool destination = true;
        if (scenario == 2) ClearGeneric(out destination);
        else CopyGeneric(ref value, out destination);
        return (value ? 2 : 0) + (destination ? 1 : 0);
    }

    public static int GenericStringLocations(int scenario)
    {
        string first = scenario == 0 ? null : "content";
        string second = "other";
        ref string selected = ref (scenario == 1 ? ref second : ref first);
        CopyGeneric(ref selected, out string result);
        int before = result == null ? 0 : result.Length;
        ClearGeneric(out selected);
        return 10 * before + (first == null ? 2 : 0) + (second == null ? 1 : 0);
    }

    private static void CopyGeneric<T>(ref T source, out T destination) => destination = source;
    private static void ClearGeneric<T>(out T value) => value = default;
}
