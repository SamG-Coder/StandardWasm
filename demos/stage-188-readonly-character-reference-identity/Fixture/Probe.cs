using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ReadOnlyCharacterReferenceIdentity;

public static class Probe
{
    public static int StringInterior()
    {
        ReadOnlySpan<char> view = "x\uffffy".AsSpan(1, 1);
        return Unsafe.As<char, char>(ref MemoryMarshal.GetReference(view)) == '\uffff' ? 577 : 0;
    }

    public static int GenericArrayInterior()
    {
        char[] data = ['a', 'b', 'c'];
        Span<char> writable = new Span<char>(data);
        ReadOnlySpan<char> view = writable.Slice(1, 1);
        data[1] = '\ud800';
        return Read(view) == '\ud800' ? 587 : 0;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static char Read<T>(ReadOnlySpan<T> view) =>
        Unsafe.As<T, char>(ref MemoryMarshal.GetReference(view));

    public static unsafe int NativeInterior()
    {
        char* address = stackalloc char[2];
        address[0] = 'x';
        address[1] = '\ufffe';
        Span<char> writable = new Span<char>(address, 2);
        ReadOnlySpan<char> view = writable.Slice(1, 1);
        return Unsafe.As<char, char>(ref MemoryMarshal.GetReference(view)) == '\ufffe' ? 593 : 0;
    }

    public static int EmptyAnchors()
    {
        ReadOnlySpan<char> noOwner = default;
        ReadOnlySpan<char> anchored = "abc".AsSpan(3);
        return Unsafe.IsNullRef(ref Unsafe.As<char, char>(ref MemoryMarshal.GetReference(noOwner))) &&
            !Unsafe.IsNullRef(ref Unsafe.As<char, char>(ref MemoryMarshal.GetReference(anchored))) ? 599 : 0;
    }

    public static int WritableAlias()
    {
        char[] data = ['a'];
        Span<char> view = new Span<char>(data);
        Unsafe.As<char, char>(ref MemoryMarshal.GetReference(view)) = 'Z';
        return data[0] == 'Z' ? 601 : 0;
    }

    public static int UnverifiedWriter()
    {
        char[] data = ['a'];
        Span<char> writable = new Span<char>(data);
        WriteThrough(writable);
        return data[0] == 'Z' ? 607 : 0;
    }

    public static int UnverifiedRefCall()
    {
        ReadOnlySpan<char> view = "x".AsSpan();
        return CallThrough(view) == 'x' ? 613 : 0;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void WriteThrough(ReadOnlySpan<char> view) =>
        Unsafe.As<char, char>(ref MemoryMarshal.GetReference(view)) = 'Z';

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static char CallThrough(ReadOnlySpan<char> view) =>
        ReadReference(ref Unsafe.As<char, char>(ref MemoryMarshal.GetReference(view)));

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static char ReadReference(ref char value) => value;
}
