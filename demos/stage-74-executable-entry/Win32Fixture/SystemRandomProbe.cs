using System;
using System.Runtime.InteropServices;

public static unsafe class SystemRandomProbe
{
    [DllImport("BCrypt.dll", ExactSpelling = true)]
    private static extern int BCryptGenRandom(IntPtr algorithm, byte* buffer, int length, int flags);

    public static int CheckSystemRandomBuffer(int length)
    {
        byte* buffer = stackalloc byte[length + 2];
        for (int i = 0; i < length + 2; i++) buffer[i] = 0xa5;
        int status = BCryptGenRandom(IntPtr.Zero, buffer + 1, length, 2);
        if (status != 0) return status;
        if (buffer[0] != 0xa5 || buffer[length + 1] != 0xa5) return -1;
        for (int start = 0; start < length; start += 65536)
        {
            int end = start + 65536;
            if (end > length) end = length;
            bool changed = false;
            for (int index = start; index < end; index++)
                if (buffer[index + 1] != 0xa5) changed = true;
            if (!changed) return -2;
        }
        return 0;
    }

    public static int CheckSystemRandomStatus(int scenario)
    {
        byte* buffer = stackalloc byte[16];
        if (scenario == 0) return BCryptGenRandom(IntPtr.Zero, null, 0, 2);
        return BCryptGenRandom(IntPtr.Zero, buffer, 16, scenario == 1 ? 0 : scenario == 2 ? 4 : 3);
    }

    // The Wasm runner supplies addresses outside its memory for this probe.
    public static int CheckSystemRandomRange(byte* buffer, int length) =>
        BCryptGenRandom(IntPtr.Zero, buffer, length, 2);
}
