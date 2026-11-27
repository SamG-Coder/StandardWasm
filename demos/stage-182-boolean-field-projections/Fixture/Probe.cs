using System;
using System.Runtime.CompilerServices;

namespace BooleanFieldProjections;

public struct Entry<T>
{
    public T Flag;
    public int Stamp;
}

public struct Container<T>
{
    public Entry<T> Item;
    public int Marker;
}

public struct EntryWithStatic<T>
{
    public static int Shared;
    public T Flag;
    public int Stamp;
}

public static class Probe
{
    public static int ClosedArrayAliases()
    {
        var entries = new Entry<bool>[2];
        entries[0] = new Entry<bool> { Flag = true, Stamp = 9 };
        entries[1] = new Entry<bool> { Flag = false, Stamp = 13 };
        ref bool first = ref FieldAt(entries, 0);
        ref bool same = ref FieldAt(entries, 0);
        ref bool different = ref FieldAt(entries, 1);
        Entry<bool> copy = entries[0];

        entries[0] = new Entry<bool> { Flag = false, Stamp = 37 };
        bool old = Exchange(ref first, true);
        bool sameObserved = same;
        same = false;
        bool oldDifferent = Exchange(ref different, true);
        return !old && sameObserved && !first && !oldDifferent && entries[1].Flag &&
            copy.Flag && copy.Stamp == 9 && entries[0].Stamp == 37 && entries[1].Stamp == 13 ? 487 : 0;
    }

    public static int MixedStorage()
    {
        bool local = true;
        var entries = new Entry<bool>[1];
        ref bool projected = ref FieldAt(entries, 0);
        bool firstLocal = Exchange(ref local, false);
        bool firstField = Exchange(ref projected, true);
        bool secondLocal = Exchange(ref local, true);
        bool secondField = Exchange(ref projected, false);
        return firstLocal && !firstField && !secondLocal && secondField && local && !entries[0].Flag ? 491 : 0;
    }

    public static int NestedGenericReset()
    {
        var entries = new Container<bool>[1];
        ref Entry<bool> inner = ref entries[0].Item;
        ref bool projected = ref inner.Flag;
        entries[0] = new Container<bool>
        {
            Item = new Entry<bool> { Flag = true, Stamp = 31 },
            Marker = 41
        };
        bool before = projected;
        Reset(ref projected);
        return before && !inner.Flag && entries[0].Item.Stamp == 31 && entries[0].Marker == 41 ? 499 : 0;
    }

    public static int StaticFieldBeforeFlag()
    {
        var entries = new EntryWithStatic<bool>[1];
        EntryWithStatic<bool>.Shared = 71;
        entries[0] = new EntryWithStatic<bool> { Flag = true, Stamp = 73 };
        ref bool flag = ref entries[0].Flag;
        bool before = Exchange(ref flag, false);
        return before && !flag && !entries[0].Flag && entries[0].Stamp == 73 &&
            EntryWithStatic<bool>.Shared == 71 ? 509 : 0;
    }

    public static int NullArray()
    {
        Entry<bool>[] entries = null;
        try { return Exchange(ref FieldAt(entries, 0), true) ? 1 : 0; }
        catch (NullReferenceException) { return 503; }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static ref T FieldAt<T>(Entry<T>[] entries, int index) => ref entries[index].Flag;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool Exchange(ref bool location, bool value)
    {
        bool before = location;
        location = value;
        return before;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Reset<T>(ref T value) => value = default;
}
