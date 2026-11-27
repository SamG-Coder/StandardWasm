using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace ClassInt32Exchange;

public class Holder
{
    public int Value;
}

public sealed class Derived : Holder { }

public struct Snapshot
{
    public int Value;
    public int Other;
}

public static class Probe
{
    private static int staticValue;

    public static int MixedLocations()
    {
        var holder = new Derived();
        var different = new Holder();
        int[] array = [17];
        int local = 19;
        staticValue = 23;
        ref int alias = ref Address(holder);
        ref int secondAlias = ref Address(holder);

        holder.Value = int.MinValue;
        int oldField = Swap(ref alias, 31);
        int oldArray = Swap(ref array[0], -37);
        int oldLocal = Swap(ref local, 41);
        int oldStatic = Swap(ref staticValue, 43);
        int oldOther = Swap(ref different.Value, 47);
        secondAlias = 53;
        return oldField == int.MinValue && oldArray == 17 && oldLocal == 19 && oldStatic == 23 && oldOther == 0 &&
            alias == 53 && holder.Value == 53 && different.Value == 47 && array[0] == -37 &&
            local == 41 && staticValue == 43 ? 211 : 0;
    }

    public static int ClassAndAggregateLocations()
    {
        var holder = new Holder();
        var snapshots = new Snapshot[1];
        ref int field = ref Address(holder);
        ref int projected = ref snapshots[0].Value;
        Swap(ref field, 13);
        Swap(ref projected, 17);
        snapshots[0] = new Snapshot { Value = -19, Other = 23 };
        int oldField = Swap(ref field, 29);
        int oldProjected = Swap(ref projected, 31);
        return oldField == 13 && oldProjected == -19 && holder.Value == 29 &&
            snapshots[0].Value == 31 && snapshots[0].Other == 23 ? 223 : 0;
    }

    public static int NullLocation()
    {
        try { return Swap(ref Unsafe.NullRef<int>(), 5); }
        catch (NullReferenceException) { return 227; }
    }

    public static int NullOwner()
    {
        Holder missing = null!;
        try { return Swap(ref Address(missing), 7); }
        catch (NullReferenceException) { return 229; }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static ref int Address(Holder holder) => ref holder.Value;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int Swap(ref int cell, int value) => Interlocked.Exchange(ref cell, value);
}
