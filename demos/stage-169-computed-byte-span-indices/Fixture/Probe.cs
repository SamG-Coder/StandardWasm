using System;
using System.Runtime.CompilerServices;

namespace ComputedByteSpanIndices;

public static class Probe
{
    private static int observed;

    public static int AliasedSlice()
    {
        byte[] source = [3, 17, 37, 97, 113, 131];
        ReadOnlySpan<byte> slice = MakeSlice(source, 1, 4);
        source[4] = 233;
        return ReadMasked(slice, -1, 1);
    }

    public static int SliceBounds()
    {
        byte[] source = [3, 17, 37, 97, 113, 131];
        ReadOnlySpan<byte> slice = MakeSlice(source, 1, 2);
        try { return ReadMasked(slice, -1, 1); }
        catch (IndexOutOfRangeException) { return 271; }
    }

    public static int NegativeIndex()
    {
        byte[] source = [3, 17, 37];
        ReadOnlySpan<byte> span = new ReadOnlySpan<byte>(source);
        try { return ReadOffset(span, -2, 1); }
        catch (IndexOutOfRangeException) { return 277; }
    }

    public static unsafe int NativePointerIndex()
    {
        byte* entropy = stackalloc byte[3];
        entropy[1] = 197;
        byte[] source = [17, 37, 97, 201];
        ReadOnlySpan<byte> span = new ReadOnlySpan<byte>(source);
        return span[(entropy[1] & 3) | ((entropy[1] >> 6) & 2)];
    }

    public static int SideEffectingIndex()
    {
        observed = 0;
        byte[] source = [17, 37, 97];
        ReadOnlySpan<byte> span = new ReadOnlySpan<byte>(source);
        return ReadWithCall(span, 1) == 37 && observed == 1 ? 281 : 0;
    }

    public static int BranchingIndex()
    {
        byte[] source = [17, 37, 97];
        ReadOnlySpan<byte> span = new ReadOnlySpan<byte>(source);
        return ReadWithBranch(span, false, 1, 2) == 97 ? 283 : 0;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static ReadOnlySpan<byte> MakeSlice(byte[] source, int start, int length)
    {
        ReadOnlySpan<byte> span = new ReadOnlySpan<byte>(source);
        return span.Slice(start, length);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int ReadMasked(ReadOnlySpan<byte> span, int low, int high) =>
        span[(low & 1) | ((high & 1) << 1)];

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int ReadOffset(ReadOnlySpan<byte> span, int index, int displacement) =>
        span[index + displacement];

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int ReadWithCall(ReadOnlySpan<byte> span, int index) => span[Observe(index)];

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int ReadWithBranch(ReadOnlySpan<byte> span, bool first, int left, int right) =>
        span[first ? left : right];

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int Observe(int index)
    {
        observed++;
        return index;
    }
}
