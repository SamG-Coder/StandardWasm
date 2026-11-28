using System;

public static class Guest
{
    public static int Signed(long value) => unchecked((int)new IntPtr(value));
    public static uint Unsigned(ulong value) => unchecked((uint)new UIntPtr(value));
    public static int Masked(nint value) => unchecked((int)new IntPtr((long)(value & -2)));

    public static int Caught(long value)
    {
        try
        {
            return unchecked((int)new IntPtr(value));
        }
        catch (OverflowException)
        {
            return 739;
        }
    }

    public static unsafe int SignedPointer(int value) => unchecked((int)new IntPtr((void*)value));
    public static unsafe uint UnsignedPointer(uint value) => unchecked((uint)new UIntPtr((void*)value));
}
