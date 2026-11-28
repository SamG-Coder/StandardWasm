using System;
using System.Collections;

public static class Guest
{
    public static int StringMembership() =>
        IsConvertible("string") && IsComparable("string") ? 101 : 0;

    public static int StringCastIdentity()
    {
        object source = "string";
        return CastComparable(source) == source ? 103 : 0;
    }

    public static int Null()
    {
        object source = null;
        return !IsConvertible(source) && CastComparable(source) == null ? 107 : 0;
    }

    public static int ClassAndPrimitiveBox()
    {
        object box = 42;
        object comparable = new Comparable();
        return IsConvertible(box) && IsComparable(box) &&
            IsComparable(comparable) && !IsConvertible(comparable) &&
            CastComparable(comparable) == comparable ? 109 : 0;
    }

    public static int InvalidCast()
    {
        try
        {
            CastComparable(new object());
            return 0;
        }
        catch (InvalidCastException)
        {
            return 113;
        }
    }

    // String and Array both implement IEnumerable. Arrays use a separate
    // representation whose dedicated membership test is still unavailable.
    public static int StringAndArrayInterface(object value) => value is IEnumerable ? 127 : 0;

    private static bool IsConvertible(object value) => value is IConvertible;
    private static bool IsComparable(object value) => value is IComparable;
    private static IComparable CastComparable(object value) => (IComparable)value;

    private sealed class Comparable : IComparable
    {
        public int CompareTo(object other) => 0;
    }
}
