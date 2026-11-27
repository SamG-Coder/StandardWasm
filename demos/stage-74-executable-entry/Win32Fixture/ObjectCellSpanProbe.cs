using System;

public static class ObjectCellSpanProbe
{
    public static int ObjectSpanAliases(int scenario)
    {
        object first = new object();
        object second = scenario == 0 ? null : new object();
        return ArgumentAlias(first, second) * 10 + LocalAlias(first, second);
    }

    private static int ArgumentAlias(object value, object next)
    {
        var earlier = new ReadOnlySpan<object>(in value);
        var later = new ReadOnlySpan<object>(in value);
        if (earlier.Length != 1 || earlier[0] != value) return 1;
        value = next;
        return earlier[0] == next && later[0] == next && value == next ? 2 : 3;
    }

    private static int LocalAlias(object first, object next)
    {
        object value = first;
        var view = new ReadOnlySpan<object>(in value);
        if (view[0] != first) return 4;
        value = next;
        return view[0] == next && value == next ? 5 : 6;
    }

    public static int ObjectSpanBounds(int index)
    {
        object value = null;
        var view = new ReadOnlySpan<object>(in value);
        try { return view[index] == null ? 7 : 8; }
        catch (IndexOutOfRangeException) { return 9; }
    }
}
