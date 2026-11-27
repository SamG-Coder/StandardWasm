using System;
using System.Runtime.InteropServices;

namespace DynamicCallFixture;

public static unsafe class Calls
{
    public static uint Invoke(nint target, int input, char* address) =>
        ((delegate* unmanaged<int, char*, uint>)target)(input, address);

    public static uint Pinned(nint target, int input)
    {
        var data = new char[1];
        data[0] = 'A';
        uint result = Pin(target, input, ref MemoryMarshal.GetReference(new Span<char>(data)));
        return data[0] == 'B' ? result : 0;
    }

    private static uint Pin(nint target, int input, ref char data)
    {
        fixed (char* address = &data)
            return Invoke(target, input, address);
    }

    public static int Unsupported(nint target, int input, char* address) =>
        ((delegate* unmanaged<int, char*, int>)target)(input, address);
}
