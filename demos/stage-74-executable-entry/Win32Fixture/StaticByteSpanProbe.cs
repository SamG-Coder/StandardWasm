using System;

public static class StaticByteSpanProbe
{
    private static ReadOnlySpan<byte> LookupBytes => [0, 1, 165, 255];

    public static int ReadStaticByteSpan(int index)
    {
        var bytes = LookupBytes;
        try { return 1000 * bytes.Length + bytes[index]; }
        catch (IndexOutOfRangeException) { return -1; }
    }
}
