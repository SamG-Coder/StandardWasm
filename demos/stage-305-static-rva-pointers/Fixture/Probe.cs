using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace StaticRvaPointersFixture;

internal interface IIdentifier
{
    static abstract ref readonly Guid Id { get; }
}

internal readonly struct First : IIdentifier
{
    static ref readonly Guid IIdentifier.Id
    {
        get
        {
            ReadOnlySpan<byte> data = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16];
            return ref Unsafe.As<byte, Guid>(ref MemoryMarshal.GetReference(data));
        }
    }
}

internal readonly struct Second : IIdentifier
{
    static ref readonly Guid IIdentifier.Id
    {
        get
        {
            ReadOnlySpan<byte> data = [99, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 61];
            return ref Unsafe.As<byte, Guid>(ref MemoryMarshal.GetReference(data));
        }
    }
}

internal readonly struct Managed : IIdentifier
{
    private static Guid storage;
    static ref readonly Guid IIdentifier.Id => ref storage;
}

public static unsafe class Probe
{
    public static int OriginalBytes()
    {
        byte* pointer = (byte*)Address<First>();
        return pointer[0] * 100 + pointer[15];
    }

    public static int StableAndDistinctAddresses()
    {
        byte* first = (byte*)Address<First>();
        byte* second = (byte*)Address<Second>();
        byte* firstAgain = (byte*)AddressAgain<First>();
        return first == firstAgain && first != second ? second[0] * 100 + second[15] : -1;
    }

    public static int ManagedAddress() => Address<Managed>() == null ? -1 : 107;

    private static Guid* Address<T>() where T : IIdentifier => (Guid*)Unsafe.AsPointer(ref Unsafe.AsRef(in T.Id));

    private static Guid* AddressAgain<T>() where T : IIdentifier => (Guid*)Unsafe.AsPointer(ref Unsafe.AsRef(in T.Id));
}
