using System;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

public static class VectorWordProbe
{
    private static Vector128<ushort> Echo(Vector128<ushort> value) => value;
    private static Vector128<ushort> Select(bool useFirst, Vector128<ushort> first, Vector128<ushort> second) =>
        useFirst ? first : second;

    public static int UnsignedLane() => Echo(Vector128.Create((ushort)65535)).GetElement(7);

    public static int Copy()
    {
        Vector128<ushort> source = Vector128.Create((ushort)32768);
        Vector128<ushort> snapshot = Echo(source);
        source = Vector128.Create((ushort)7);
        return (snapshot.GetElement(0) << 8) | source.GetElement(1);
    }

    private static int Reset(Vector128<ushort> value)
    {
        value = default;
        return value.GetElement(3);
    }

    public static int Default() => Reset(Vector128.Create((ushort)123));
    public static int BranchLeft() => Select(true, Vector128.Create((ushort)255), Vector128.Create((ushort)3)).GetElement(3);
    public static int BranchRight() => Select(false, Vector128.Create((ushort)255), Vector128.Create((ushort)3)).GetElement(4);

    public static int MixedSpillBanks()
    {
        var marker = new Marker(2);
        ReadOnlySpan<char> span = "A".AsSpan();
        ref char first = ref MemoryMarshal.GetReference(span);
        var vector = Echo(Vector128.Create((ushort)10));
        return first + span.Length + vector.GetElement(2) + marker.Value;
    }

    public static int ForeignLane() => Vector128.Create((short)-2).GetElement(0);
    public static int InvalidLane() => Vector128.Create((ushort)1).GetElement(8);
    public static Vector128<ushort> ExportedVector() => Vector128.Create((ushort)17);
    public static object BoxedVector() => Vector128.Create((ushort)19);
    public static int DynamicLane() => ReadLane(Vector128.Create((ushort)77), 1);

    private static ushort ReadLane(Vector128<ushort> value, int index) => value.GetElement(index);

    private sealed class Marker
    {
        internal readonly int Value;

        internal Marker(int value) => Value = value;
    }
}
