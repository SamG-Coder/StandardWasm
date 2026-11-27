using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

public static class GenericCharacterProbe
{
    private static int Read<T>(ref char input, int offset) where T : struct => Unsafe.Add(ref input, offset);

    private static int Hop<T>(ref char input) where T : struct
    {
        ref char next = ref Unsafe.Add(ref input, 1);
        return Read<T>(ref next, 1);
    }

    private static int MaybeMutate<T>(ref char input)
    {
        if (typeof(T) == typeof(int)) input = 'Z';
        return input;
    }

    private static void Write<T>(ref char input) => input = 'Q';
    private static ref char Escape<T>(ref char input) => ref input;

    public static int ReadScalar() => Hop<ushort>(ref MemoryMarshal.GetReference("AB\u03a9".AsSpan()));
    public static int ReadSimd() => Hop<Vector128<ushort>>(ref MemoryMarshal.GetReference("A!\u00e9".AsSpan()));

    public static int SiblingSpecializations()
    {
        int read = MaybeMutate<short>(ref MemoryMarshal.GetReference("A".AsSpan()));
        char[] array = ['X'];
        int wrote = MaybeMutate<int>(ref MemoryMarshal.GetReference(array.AsSpan()));
        return (read << 16) | (wrote << 8) | array[0];
    }

    public static int ReversedOrder()
    {
        char[] array = ['X'];
        int wrote = MaybeMutate<int>(ref MemoryMarshal.GetReference(array.AsSpan()));
        int read = MaybeMutate<short>(ref MemoryMarshal.GetReference("B".AsSpan()));
        return (read << 16) | (wrote << 8) | array[0];
    }

    public static int RejectedWrite()
    {
        char[] array = ['X', 'Y'];
        ReadOnlySpan<char> input = array.AsSpan();
        Write<ushort>(ref MemoryMarshal.GetReference(input));
        return array[0];
    }

    public static int RejectedEscape() => Escape<ushort>(ref MemoryMarshal.GetReference("A".AsSpan()));
}
