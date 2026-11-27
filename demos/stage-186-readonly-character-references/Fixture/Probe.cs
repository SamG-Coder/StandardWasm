using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ReadOnlyCharacterReferences;

public static class Probe
{
    public static int StringInterior()
    {
        ReadOnlySpan<char> view = "x\uffffy".AsSpan(1, 1);
        return MemoryMarshal.GetReference(view) == '\uffff' ? 521 : 0;
    }

    public static int ArrayInterior()
    {
        char[] data = ['a', 'b', 'c'];
        Span<char> writable = new Span<char>(data);
        ReadOnlySpan<char> view = writable.Slice(1, 1);
        data[1] = '\ude00';
        return MemoryMarshal.GetReference(view) == '\ude00' ? 523 : 0;
    }

    public static int ManagedEmptyAnchors()
    {
        ReadOnlySpan<char> noOwner = default;
        ReadOnlySpan<char> emptyString = "".AsSpan();
        ReadOnlySpan<char> pastEnd = "abc".AsSpan(3);
        char[] chars = new char[0];
        Span<char> writable = new Span<char>(chars);
        ReadOnlySpan<char> emptyArray = writable;
        return Unsafe.IsNullRef(ref MemoryMarshal.GetReference(noOwner)) &&
            !Unsafe.IsNullRef(ref MemoryMarshal.GetReference(emptyString)) &&
            !Unsafe.IsNullRef(ref MemoryMarshal.GetReference(pastEnd)) &&
            !Unsafe.IsNullRef(ref MemoryMarshal.GetReference(emptyArray)) ? 541 : 0;
    }

    public static unsafe int NativeAnchors()
    {
        char* address = stackalloc char[3];
        address[0] = 'a';
        address[1] = '\uffff';
        address[2] = 'c';
        Span<char> writable = new Span<char>(address, 3);
        ReadOnlySpan<char> view = writable.Slice(1, 1);
        ReadOnlySpan<char> end = writable.Slice(3, 0);
        char* zero = NullAddress();
        Span<char> nativeDefault = new Span<char>(zero, 0);
        ReadOnlySpan<char> nullView = nativeDefault;
        return zero == null && MemoryMarshal.GetReference(view) == '\uffff' &&
            !Unsafe.IsNullRef(ref MemoryMarshal.GetReference(end)) &&
            Unsafe.IsNullRef(ref MemoryMarshal.GetReference(nullView)) ? 547 : 0;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static unsafe char* NullAddress() => null;

    public static int WritableAlias()
    {
        char[] data = ['x'];
        Span<char> writable = new Span<char>(data);
        MemoryMarshal.GetReference(writable) = 'Z';
        return data[0] == 'Z' ? 569 : 0;
    }

    public static int UnverifiedRefCall()
    {
        ReadOnlySpan<char> view = "x".AsSpan();
        return CallThrough(view) == 'x' ? 571 : 0;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static char CallThrough(ReadOnlySpan<char> view) =>
        Consume(ref MemoryMarshal.GetReference(view));

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static char Consume(ref char value) => value;

    public static int UnverifiedWriter()
    {
        char[] data = ['a'];
        Span<char> writable = new Span<char>(data);
        WriteThrough(writable);
        return data[0] == 'Z' ? 557 : 0;
    }

    public static int UnverifiedEscape()
    {
        ReadOnlySpan<char> view = "a".AsSpan();
        return Escape(view) == 'a' ? 563 : 0;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void WriteThrough(ReadOnlySpan<char> view) => MemoryMarshal.GetReference(view) = 'Z';

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static ref char Escape(ReadOnlySpan<char> view) => ref MemoryMarshal.GetReference(view);
}
