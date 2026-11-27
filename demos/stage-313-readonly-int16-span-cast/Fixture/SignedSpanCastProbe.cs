using System;
using System.Runtime.InteropServices;

public static class SignedSpanCastProbe
{
    private static int Read(ReadOnlySpan<short> view, int index) => view[index];

    public static int Positive() => Read(MemoryMarshal.Cast<char, short>("\u7fff\u8000\uffff".AsSpan()), 0);
    public static int Negative() => Read(MemoryMarshal.Cast<char, short>("\u7fff\u8000\uffff".AsSpan()), 1);
    public static int NegativeOne() => Read(MemoryMarshal.Cast<char, short>("\u7fff\u8000\uffff".AsSpan()), 2);

    public static int Alias()
    {
        char[] source = ['a', 'b', 'c'];
        ReadOnlySpan<char> readonlySource = source.AsSpan();
        var view = MemoryMarshal.Cast<char, short>(readonlySource).Slice(1, 1);
        source[1] = '\ufffe';
        return Read(view, 0);
    }

    public static int Empty() => MemoryMarshal.Cast<char, short>(default(ReadOnlySpan<char>)).Length;

    public static int Bounds()
    {
        try
        {
            var view = MemoryMarshal.Cast<char, short>("XYZ".AsSpan().Slice(1, 1));
            return Read(view, 1);
        }
        catch (IndexOutOfRangeException) { return 101; }
    }

    public static int Writable()
    {
        char[] chars = ['a', 'b'];
        var writable = MemoryMarshal.Cast<char, short>(chars.AsSpan());
        writable[0] = -2;
        return chars[0];
    }
}
