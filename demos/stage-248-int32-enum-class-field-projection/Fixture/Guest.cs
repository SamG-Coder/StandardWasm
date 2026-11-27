using System;

public static class Guest
{
    enum Signed : int { Default }
    enum Unsigned : uint { Default }
    enum Narrow : byte { Default }
    enum Wide : long { Default }

    sealed class Holder
    {
        internal Signed First;
        internal Signed Second;
        internal Unsigned Unsigned;
        internal Narrow Narrow;
        internal Wide Wide;
    }

    struct ValueHolder { internal Signed Mode; }

    public static int ClassSigned()
    {
        var first = new Holder { First = (Signed)(-13), Second = (Signed)19 };
        var second = new Holder { First = (Signed)17 };
        ref Signed alias = ref first.First;
        Signed old = Replace(ref alias, (Signed)29);
        bool writtenThroughAlias = (int)first.First == 29;
        second.First = (Signed)31;
        first.First = (Signed)37;
        return (int)old == -13 && writtenThroughAlias && (int)alias == 37 &&
            (int)first.Second == 19 && (int)second.First == 31 ? 101 : 0;
    }

    public static int ClassUnsigned()
    {
        var value = new Holder();
        SetUnsigned(out value.Unsigned);
        return (uint)value.Unsigned == 0xf1234567 ? 103 : 0;
    }

    public static int ValueControl()
    {
        var value = new ValueHolder { Mode = (Signed)41 };
        Signed old = Replace(ref value.Mode, (Signed)43);
        return (int)old == 41 && (int)value.Mode == 43 ? 107 : 0;
    }

    public static int NarrowClass()
    {
        var value = new Holder();
        SetNarrow(ref value.Narrow);
        return (byte)value.Narrow == 253 ? 109 : 0;
    }

    public static int WideClass()
    {
        var value = new Holder();
        SetWide(ref value.Wide);
        return (long)value.Wide == 0x7f11223344556677 ? 113 : 0;
    }

    public static int NullReceiver()
    {
        Holder value = null;
        try { Replace(ref value.First, (Signed)127); }
        catch (NullReferenceException) { return 117; }
        return 0;
    }

    private static Signed Replace(ref Signed value, Signed replacement)
    {
        Signed old = value;
        value = replacement;
        return old;
    }

    private static void SetUnsigned(out Unsigned value) => value = (Unsigned)0xf1234567;
    private static void SetNarrow(ref Narrow value) => value = (Narrow)253;
    private static void SetWide(ref Wide value) => value = (Wide)0x7f11223344556677;
}
