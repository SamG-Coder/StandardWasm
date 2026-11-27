using System;
using System.Runtime.CompilerServices;

namespace CharacterSpanSearch;

public static class Probe
{
    private static int evaluated;

    public static int StringSlice()
    {
        ReadOnlySpan<char> view = "X\0\uffffQX".AsSpan(1, 3);
        return Search(view, 'Q', '\0') == 0 && Search(view, '\uffff', '\uffff') == 1 &&
            Search(view, 'X', 'Y') == -1 ? 449 : 0;
    }

    public static int ArrayAlias()
    {
        char[] data = ['X', 'a', 'b', 'Z'];
        Span<char> writable = new Span<char>(data);
        ReadOnlySpan<char> view = writable.Slice(1, 2);
        data[2] = '\0';
        return Search(view, 'b', '\0') == 1 && Search(view, 'X', 'Z') == -1 ? 457 : 0;
    }

    public static int EmptyAndNoMatch()
    {
        ReadOnlySpan<char> empty = default;
        ReadOnlySpan<char> anchored = "abc".AsSpan(3);
        ReadOnlySpan<char> full = "abc".AsSpan();
        return Search(empty, '\0', 'c') == -1 && Search(anchored, 'c', 'c') == -1 &&
            Search(full, '\0', '\uffff') == -1 ? 461 : 0;
    }

    public static unsafe int NativeView()
    {
        char* address = stackalloc char[5];
        address[0] = 'X';
        address[1] = 'a';
        address[2] = '\uffff';
        address[3] = '\0';
        address[4] = 'Z';
        Span<char> writable = new Span<char>(address, 5);
        ReadOnlySpan<char> window = writable.Slice(1, 3);
        ReadOnlySpan<char> empty = writable.Slice(5);
        return Search(window, '\0', '\uffff') == 1 && Search(window, 'X', 'Z') == -1 &&
            Search(empty, '\0', 'Z') == -1 ? 463 : 0;
    }

    public static int NeedlesEvaluatedBeforeEmptySearch()
    {
        evaluated = 0;
        ReadOnlySpan<char> empty = default;
        return empty.IndexOfAny(Needle(1), Needle(2)) == -1 && evaluated == 12 ? 467 : 0;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int Search(ReadOnlySpan<char> view, char first, char second) =>
        view.IndexOfAny(first, second);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static char Needle(int value)
    {
        evaluated = evaluated * 10 + value;
        return (char)value;
    }
}
