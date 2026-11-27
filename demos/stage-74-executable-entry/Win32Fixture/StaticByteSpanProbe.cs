using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

public static class StaticByteSpanProbe
{
    private static ReadOnlySpan<byte> LookupBytes => [0, 1, 165, 255];

    public static int ReadStaticByteSpan(int index)
    {
        var bytes = LookupBytes;
        try { return 1000 * bytes.Length + bytes[index]; }
        catch (IndexOutOfRangeException) { return -1; }
    }

    public static int ReadStaticByteLocation(int index)
    {
        ref byte start = ref MemoryMarshal.GetReference(LookupBytes);
        ref byte item = ref Unsafe.AddByteOffset(ref start, (nuint)index);
        return ReadPair(ref item) + item * 65536;
    }

    private static int ReadPair(ref byte item) => Unsafe.ReadUnaligned<ushort>(ref item);

    public static int CheckCoreWhitespace(int value) => char.IsWhiteSpace((char)value) ? 1 : 0;
}
