using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

public static class EmptySpanProbe
{
    private static int LengthOf(ReadOnlySpan<char> span) => span.Length;
    private static int LengthWhen(bool empty) =>
        LengthOf(empty ? ReadOnlySpan<char>.Empty : "AB".AsSpan());

    public static int Length() => ReadOnlySpan<char>.Empty.Length;
    public static int IsEmpty() => ReadOnlySpan<char>.Empty.IsEmpty ? 1 : 0;
    public static int NullAnchor() => Unsafe.IsNullRef(ref MemoryMarshal.GetReference(ReadOnlySpan<char>.Empty)) ? 1 : 0;
    public static int ZeroSlice() => ReadOnlySpan<char>.Empty.Slice(0, 0).Length;
    public static int BothBranches() => LengthWhen(true) * 10 + LengthWhen(false);

    public static int RejectIndex()
    {
        try { return ReadOnlySpan<char>.Empty[0]; }
        catch (IndexOutOfRangeException) { return 91; }
    }

    public static int OtherLane() => ReadOnlySpan<short>.Empty.Length;
}
