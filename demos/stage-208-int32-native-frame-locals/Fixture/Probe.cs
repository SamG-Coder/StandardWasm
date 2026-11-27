using System.Runtime.CompilerServices;

namespace Int32NativeFrameLocals;

public static unsafe class Probe
{
    public static int SignedAlias()
    {
        int value = -9;
        int* alias = &value;
        int first = *alias;
        Set(alias, int.MinValue);
        int afterCall = value;
        value = 1234;
        return first == -9 && afterCall == int.MinValue && *alias == 1234 ? 773 : 0;
    }

    public static int UnsignedAlias()
    {
        uint value = 0xf000ffffu;
        uint* alias = &value;
        uint first = *alias;
        *alias = 0xe0000000u;
        uint shifted = value >> 4;
        value = uint.MaxValue;
        return first == 0xf000ffffu && shifted == 0x0e000000u && *alias == uint.MaxValue ? 787 : 0;
    }

    public static int ReusedFrame()
    {
        // This many calls would exhaust the shadow stack if a frame leaked.
        for (int iteration = 0; iteration < 70000; iteration++)
            if (Word(true) != 123456 || Word(false) != 0) return 0;
        return 797;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int Word(bool fill)
    {
        int local;
        int* alias = &local;
        if (fill) *alias = 123456;
        return *alias;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Set(int* target, int value) => *target = value;

    public static int IndependentCells()
    {
        int first = 7;
        uint second = 8;
        int* left = &first;
        uint* right = &second;
        *left = -98765;
        *right = 0x87654321u;
        second = 12;
        return first == -98765 && *left == -98765 && *right == 12 ? 809 : 0;
    }

    public static int MixedAddress()
    {
        int value = 123;
        int* alias = &value;
        SetManaged(ref value);
        return *alias == 42 ? 811 : 0;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void SetManaged(ref int target) => target = 42;

    public static int BooleanAddress()
    {
        bool value = false;
        bool* alias = &value;
        *alias = true;
        return value ? 821 : 0;
    }

    public static int ShortAddress()
    {
        short value = 0;
        short* alias = &value;
        *alias = -302;
        return value == -302 ? 823 : 0;
    }
}
