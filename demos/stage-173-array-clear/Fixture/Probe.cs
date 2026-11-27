using System;

namespace ManagedArrayClear;

public static class Probe
{
    private struct Entry<T>
    {
        public T Value;
        public int Count;
    }

    private struct Payload
    {
        public string Text;
        public int Count;
    }

    public static int WholeNumeric()
    {
        var integers = new int[3];
        integers[0] = 13;
        integers[2] = -77;
        var doubles = new double[1];
        doubles[0] = -2.5;
        var wide = new long[1];
        wide[0] = -0x123456789L;
        Array.Clear(integers);
        Array.Clear(doubles);
        Array.Clear(wide);
        return integers[0] + integers[2] + (int)wide[0] + (1.0 / doubles[0] > 0 ? 37 : -37);
    }

    public static int ReferenceRange()
    {
        var names = new string[3];
        names[0] = "keep";
        names[1] = "clear";
        names[2] = "tail";
        Array.Clear(names, 1, 1);
        return names[0].Length + names[2].Length + (names[1] == null ? 100 : -100);
    }

    public static int NetStandardPacked() => NetStandardRange.ClearPacked();

    public static int ReferenceAlias()
    {
        object marker = "updated";
        var values = new object[2];
        values[0] = marker;
        values[1] = marker;
        ref object alias = ref values[1];
        Array.Clear(values);
        int cleared = alias == null && values[0] == null ? 100 : -100;
        alias = marker;
        return cleared + (values[1] == marker ? 7 : -7);
    }

    public static int NumericAlias()
    {
        var values = new int[3];
        values[0] = 17;
        values[1] = 19;
        ref int view = ref values[1];
        Array.Clear(values, 1, 1);
        int cleared = view;
        view = 29;
        return values[0] * 100 + values[1] + cleared;
    }

    public static int WholeAggregate()
    {
        var entries = new Entry<int>[2];
        entries[1] = new Entry<int> { Count = 23, Value = 31 };
        ref Entry<int> view = ref entries[1];
        Entry<int> before = view;
        Array.Clear(entries);
        int cleared = view.Count + view.Value;
        view.Count = 41;
        return before.Count * 100 + before.Value + entries[1].Count + cleared;
    }

    public static int ProjectedAggregateRange()
    {
        var entries = new Entry<Payload>[3];
        entries[0] = new Entry<Payload> { Count = 13 };
        entries[1] = new Entry<Payload> { Count = 31, Value = new Payload { Count = 17, Text = "old" } };
        entries[2] = new Entry<Payload> { Count = 19 };
        ref Entry<Payload> entry = ref entries[1];
        ref Payload view = ref entry.Value;
        Payload before = view;
        Array.Clear(entries, 1, 1);
        int cleared = view.Count + entry.Count + (view.Text == null ? 0 : 90000);
        view = new Payload { Count = 47, Text = "new" };
        return before.Count * 1000 + before.Text.Length + entries[0].Count + entries[1].Value.Count +
            entries[2].Count + cleared;
    }

    public static int EmptyAtEnd()
    {
        var array = new byte[1];
        array[0] = 127;
        Array.Clear(array, array.Length, 0);
        return array[0];
    }

    public static int NullWhole()
    {
        try { Array.Clear(null); }
        catch (ArgumentNullException) { return 11; }
        return -1;
    }

    public static int NullRangeFirst()
    {
        try { Array.Clear(null, -1, -1); }
        catch (ArgumentNullException) { return 17; }
        return -1;
    }

    public static int NegativeIndex()
    {
        var array = new int[1];
        array[0] = 23;
        try { Array.Clear(array, -1, 1); }
        catch (IndexOutOfRangeException) { return array[0]; }
        return -1;
    }

    public static int NegativeLength()
    {
        try { Array.Clear(new int[1], 0, -1); }
        catch (IndexOutOfRangeException) { return 31; }
        return -1;
    }

    public static int OverflowRange()
    {
        try { Array.Clear(new int[1], int.MaxValue, int.MaxValue); }
        catch (IndexOutOfRangeException) { return 37; }
        return -1;
    }

    public static int ZeroPastEnd()
    {
        try { Array.Clear(new int[1], 2, 0); }
        catch (IndexOutOfRangeException) { return 41; }
        return -1;
    }
}
