using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ObjectReadOnlySpansFixture;

[InlineArray(3)]
[StructLayout(LayoutKind.Auto)]
public struct Slots<T>
{
    private T first;
}

public static class Kernels
{
    private static int Read(ReadOnlySpan<object> view, object marker, int index)
    {
        return view[index] == marker ? view.Length + 20 : view.Length + 30;
    }

    public static int ObserveElement(int index)
    {
        Slots<object> slots = default;
        ReadOnlySpan<object> view = slots;
        var marker = new object();
        slots[1] = marker;
        return Read(view, marker, index);
    }

    public static int ObserveReset()
    {
        Slots<object> slots = default;
        slots[1] = new object();
        ReadOnlySpan<object> view = slots;
        slots = default;
        return view[1] == null ? 17 : -1;
    }

    public static int EmptyLength()
    {
        scoped ReadOnlySpan<object> view = default;
        return view.Length;
    }

    public static int InvalidIndex(int index, int empty)
    {
        Slots<object> slots = default;
        scoped ReadOnlySpan<object> view = default;
        if (empty == 0) view = slots;
        try { return view[index] == null ? 11 : -1; }
        catch (IndexOutOfRangeException) { return 13; }
    }
}
