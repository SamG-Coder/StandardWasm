using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace InlineObjectAddressesFixture;

[InlineArray(3)]
[StructLayout(LayoutKind.Auto)]
public struct Slots<T>
{
    private T first;
}

public static class Kernels
{
    private static object AssignAndRead(ref object target, ref object alias, object value)
    {
        target = value;
        return alias;
    }

    public static int SharedAlias()
    {
        Slots<object> slots = default;
        object marker = new object();
        object result = AssignAndRead(ref slots[1], ref slots[1], marker);
        if (result != marker) return -1;
        return slots[2] == null ? 11 : -2;
    }

    public static int DefaultKeepsAlias()
    {
        Slots<object> slots = default;
        ref object alias = ref slots[1];
        alias = new object();
        slots = default;
        return alias == null ? 17 : -1;
    }

    public static int CopyAndReplace(int choose)
    {
        Slots<object> source = default;
        Slots<object> destination = default;
        ref object destinationAlias = ref destination[1];
        object marker = new object();
        source[1] = marker;
        destination = source;
        if (destinationAlias != marker) return -1;
        if (choose != 0) source[1] = null;
        if (destinationAlias != marker) return -2;
        return source[1] == marker ? 19 : 23;
    }

    private static void SetAndThrow(ref object alias, object marker, int divisor)
    {
        alias = marker;
        if (1 / divisor != 0) alias = null;
    }

    public static int Unwind(int divisor)
    {
        Slots<object> slots = default;
        object marker = new object();
        try { SetAndThrow(ref slots[2], marker, divisor); }
        catch (DivideByZeroException) { return slots[2] == marker ? 31 : -1; }
        return slots[2] == marker ? 41 : 37;
    }
}
