using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

public static class ReadOnlyCharacterProbe
{
    private static int ReadAt(ref char first, int offset) => Unsafe.Add(ref first, offset);

    private static int Forward(ref char left, ref char right, int offset, bool chooseLeft)
    {
        ref char selected = ref left;
        if (!chooseLeft) selected = ref right;
        return ReadAt(ref selected, offset);
    }

    public static int Left()
    {
        var left = "AéC!".AsSpan();
        var right = "γδζ.".AsSpan();
        return Forward(ref MemoryMarshal.GetReference(left), ref MemoryMarshal.GetReference(right), 1, true);
    }

    public static int Right()
    {
        var left = "AéC!".AsSpan();
        var right = "γδζ.".AsSpan();
        return Forward(ref MemoryMarshal.GetReference(left), ref MemoryMarshal.GetReference(right), 2, false);
    }

    public static int Backward()
    {
        var input = "xΩz".AsSpan();
        ref char middle = ref Unsafe.Add(ref MemoryMarshal.GetReference(input), 1);
        return ReadAt(ref middle, -1);
    }

    private static bool CheckEmpty(ref char start) => Unsafe.IsNullRef(ref Unsafe.Add(ref start, 0));
    public static int Empty() => CheckEmpty(ref MemoryMarshal.GetReference(default(ReadOnlySpan<char>))) ? 1 : 0;

    private static void Write(ref char first) => first = 'Z';

    public static int Writable()
    {
        char[] values = ['A', 'B'];
        Write(ref MemoryMarshal.GetReference(values.AsSpan()));
        return values[0];
    }

    public static int RejectedWrite()
    {
        char[] values = ['A', 'B'];
        ReadOnlySpan<char> wrapped = values.AsSpan();
        Write(ref MemoryMarshal.GetReference(wrapped));
        return values[0];
    }

    private static ref char Escape(ref char first) => ref first;

    public static int RejectedEscape() => Escape(ref MemoryMarshal.GetReference("AB".AsSpan()));

    public static int OutOfOwner() => ReadAt(ref MemoryMarshal.GetReference("AB".AsSpan()), int.MaxValue);
}
