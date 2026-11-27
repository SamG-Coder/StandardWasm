using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SingleCharacterLocalSpan;

public static class Probe
{
    public static int LiveLocal()
    {
        char letter = 'a';
        ReadOnlySpan<char> view = new(in letter);
        letter = '\uffff';
        return view.Length == 1 && view[0] == '\uffff' ? 631 : 0;
    }

    public static int LocalRefWrite()
    {
        char letter = 'a';
        ReadOnlySpan<char> view = new(in letter);
        ref char alias = ref letter;
        alias = '\ud800';
        return letter == '\ud800' && MemoryMarshal.GetReference(view) == '\ud800' ? 641 : 0;
    }

    public static int DistinctAnchors()
    {
        char first = 'a', second = 'a';
        ReadOnlySpan<char> a = new(in first);
        ReadOnlySpan<char> same = new(in first);
        ReadOnlySpan<char> other = new(in second);
        return a == same && a != other ? 643 : 0;
    }

    public static int EndAnchors()
    {
        char letter = '\0';
        ReadOnlySpan<char> original = new(in letter);
        ReadOnlySpan<char> end = original.Slice(1), initialEmpty = original.Slice(0, 0);
        return end.IsEmpty && !Unsafe.IsNullRef(ref MemoryMarshal.GetReference(end)) &&
            end != initialEmpty ? 647 : 0;
    }

    public static int SameFrameRead()
    {
        char letter = 'x';
        ReadOnlySpan<char> view = new(in letter);
        letter = '\ufffe';
        return Read(view) == '\ufffe' ? 653 : 0;
    }

    public static int SeparateFrames()
    {
        char letter = 'X';
        ReadOnlySpan<char> caller = new(in letter);
        return DistinctFrame(caller) ? 661 : 0;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool DistinctFrame(ReadOnlySpan<char> caller)
    {
        char letter = 'X';
        ReadOnlySpan<char> own = new(in letter);
        return own != caller;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static char Read(ReadOnlySpan<char> span) => MemoryMarshal.GetReference(span);
}
