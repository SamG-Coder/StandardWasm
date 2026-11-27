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

    public static uint PinnedArray(nint target, int input)
    {
        char[] data = ['L', 'A', 'R'];
        var suffix = new Span<char>(data).Slice(1);
        uint result = PinDirect(target, input, ref MemoryMarshal.GetReference(suffix));
        return data[0] == 'L' && data[1] == 'B' && data[2] == 'R' ? result : 0;
    }

    public static uint PinnedStack(nint target, int input)
    {
        char* data = stackalloc char[3];
        data[0] = 'L';
        data[1] = 'A';
        data[2] = 'R';
        var suffix = new Span<char>(data, 3).Slice(1);
        uint result = PinDirect(target, input, ref MemoryMarshal.GetReference(suffix));
        return data[0] == 'L' && data[1] == 'B' && data[2] == 'R' ? result : 0;
    }

    private static uint PinDirect(nint target, int input, ref char data)
    {
        uint result;
        fixed (char* address = &data)
            result = ((delegate* unmanaged<int, char*, uint>)target)(input, address);
        return result;
    }

    public static int Unsupported(nint target, int input, char* address) =>
        ((delegate* unmanaged<int, char*, int>)target)(input, address);
}
