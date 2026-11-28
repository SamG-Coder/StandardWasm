using System;

namespace ConstrainedInt32Compare;

public static class Probe
{
    private static int Compare<T>(T left, T right) where T : IComparable<T> => left.CompareTo(right);

    private static int Direct(int left, int right) => left.CompareTo(right);

    public static int Less() => Compare(-7, 9);

    public static int Equal() => Compare(-7, -7);

    public static int Greater() => Compare(9, -7);

    public static int MinToMax() => Compare(int.MinValue, int.MaxValue);

    public static int MaxToMin() => Compare(int.MaxValue, int.MinValue);

    public static int DirectCompare() => Direct(int.MinValue, int.MaxValue);

    public static int UnsignedControl() => Compare(uint.MaxValue, 0u);
}
