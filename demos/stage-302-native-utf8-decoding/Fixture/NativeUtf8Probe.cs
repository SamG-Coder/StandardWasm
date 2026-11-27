using System;
using System.Runtime.InteropServices;

public static class NativeUtf8Probe
{
    public static int Read(IntPtr address, int index)
    {
        string? value = Marshal.PtrToStringUTF8(address);
        if (value is null) return -1;
        return index < 0 ? value.Length : value[index];
    }
}
