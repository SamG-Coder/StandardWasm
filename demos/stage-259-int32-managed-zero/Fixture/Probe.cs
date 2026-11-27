using System;
using System.Runtime.CompilerServices;

public static class Probe
{
    public static int SignedLocal()
    {
        int value = int.MinValue;
        SetDefault(ref value);
        return value == 0 ? 101 : 0;
    }

    public static int UnsignedLocal()
    {
        uint value = uint.MaxValue;
        SetDefault(ref value);
        return value == 0 ? 103 : 0;
    }

    public static int ArrayAlias()
    {
        int[] values = new int[3];
        values[0] = 11;
        values[1] = 73;
        values[2] = 17;
        ref int alias = ref values[1];
        SetDefault(ref alias);
        return values[0] == 11 && alias == 0 && values[1] == 0 && values[2] == 17 ? 107 : 0;
    }

    public static int ClassFieldAlias()
    {
        var owner = new Holder();
        owner.Signed = -77;
        owner.Unsigned = uint.MaxValue;
        ref int alias = ref owner.Signed;
        SetDefault(ref alias);
        return owner.Signed == 0 && alias == 0 && owner.Unsigned == uint.MaxValue ? 109 : 0;
    }

    public static int AggregateFieldAlias()
    {
        Counter counter = default;
        counter.Value = -83;
        counter.Other = 19;
        ref int alias = ref counter.Value;
        int snapshot = alias;
        SetDefault(ref alias);
        return snapshot == -83 && alias == 0 && counter.Value == 0 && counter.Other == 19 ? 113 : 0;
    }

    public static int NullAddress()
    {
        try
        {
            SetDefault(ref Unsafe.NullRef<int>());
            return 0;
        }
        catch (NullReferenceException)
        {
            return 127;
        }
    }

    public static int BooleanAndReferenceControls()
    {
        bool flag = true;
        object reference = new object();
        SetDefault(ref flag);
        SetDefault(ref reference);
        return !flag && reference == null ? 131 : 0;
    }

    private static void SetDefault<T>(ref T value) => value = default(T);

    private sealed class Holder
    {
        public int Signed;
        public uint Unsigned;
    }

    private struct Counter
    {
        public int Value;
        public int Other;
    }
}
