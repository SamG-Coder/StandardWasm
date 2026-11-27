using System;
using System.Runtime.InteropServices;

public static class NativeByteTransferProbe
{
    public static void Transfer(IntPtr source, byte[] destination, int start, int count) =>
        Marshal.Copy(source, destination, start, count);

    public static byte[] Make(int count)
    {
        var bytes = new byte[count];
        for (int index = 0; index < bytes.Length; index++) bytes[index] = 90;
        return bytes;
    }

    public static int Read(byte[] bytes, int index) => bytes[index];
}
