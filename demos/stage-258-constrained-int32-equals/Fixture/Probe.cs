using System;
using System.Runtime.CompilerServices;

public static class Probe
{
    public static int SignedValues() =>
        EqualValue(-1, -1) && !EqualValue(-1, 1) &&
        EqualValue(int.MinValue, int.MinValue) ? 101 : 0;

    public static int BoxIdentity() =>
        !EqualValue(-1, uint.MaxValue) && !EqualValue(1, true) &&
        !EqualValue(1, null) ? 103 : 0;

    public static int LiveFieldReceiver()
    {
        var value = new Holder();
        ref int alias = ref value.Number;
        alias = 42;
        bool first = EqualReference(ref alias, 42);
        value.Number = 53;
        return first && EqualReference(ref alias, 53) &&
            !EqualReference(ref alias, 42) ? 107 : 0;
    }

    public static int NullLocationMismatch()
    {
        ref int address = ref Unsafe.NullRef<int>();
        return !EqualReference(ref address, null) &&
            !EqualReference(ref address, uint.MaxValue) ? 109 : 0;
    }

    public static int NullLocationMatch()
    {
        try
        {
            EqualReference(ref Unsafe.NullRef<int>(), 0);
            return 0;
        }
        catch (NullReferenceException)
        {
            return 113;
        }
    }

    public static int DirectAndConstrained()
    {
        int value = 42;
        object boxed = 42;
        return value.Equals(boxed) && EqualReference(ref value, boxed) ? 127 : 0;
    }

    public static int UnsignedBoundary()
    {
        uint value = uint.MaxValue;
        return EqualReference(ref value, uint.MaxValue) ? 131 : 0;
    }

    private static bool EqualValue<T>(T value, object other) => value.Equals(other);
    private static bool EqualReference<T>(ref T value, object other) => value.Equals(other);

    private sealed class Holder
    {
        public int Number;
    }
}
