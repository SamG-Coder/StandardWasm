using System;
using System.Runtime.CompilerServices;

namespace CharacterSpanIndexOf;

public static class Probe
{
    private static int evaluated;

    public static int FirstAndRelative()
    {
        ReadOnlySpan<char> view = "~\0\uffff~~X".AsSpan(1, 4);
        return Search(view, '~') == 2 && Search(view, '\uffff') == 1 &&
            Search(view, '\0') == 0 && Search(view, 'X') == -1 ? 673 : 0;
    }

    public static int ArrayAlias()
    {
        char[] data = ['X', 'a', 'b', 'X'];
        Span<char> writable = new Span<char>(data);
        ReadOnlySpan<char> view = writable.Slice(1, 2);
        data[2] = '\udfff';
        return Search(view, '\udfff') == 1 && Search(view, 'X') == -1 &&
            Search(view, 'b') == -1 ? 677 : 0;
    }

    public static int EmptyAndDefault()
    {
        ReadOnlySpan<char> empty = default;
        ReadOnlySpan<char> fromNull = ((string)null).AsSpan();
        ReadOnlySpan<char> anchored = "abc".AsSpan(3);
        return Search(empty, '\0') == -1 && Search(fromNull, '\0') == -1 &&
            Search(anchored, 'c') == -1 ? 683 : 0;
    }

    public static unsafe int NativeView()
    {
        char* data = stackalloc char[5];
        data[0] = 'X';
        data[1] = '\uffff';
        data[2] = '\0';
        data[3] = '\uffff';
        data[4] = 'X';
        Span<char> writable = new Span<char>(data, 5);
        ReadOnlySpan<char> view = writable.Slice(1, 3), anchored = writable.Slice(5);
        return Search(view, '\uffff') == 0 && Search(view, '\0') == 1 &&
            Search(view, 'X') == -1 && Search(anchored, 'X') == -1 ? 691 : 0;
    }

    public static int EvaluationOrder()
    {
        evaluated = 0;
        return EmptyView().IndexOf(Needle()) == -1 && evaluated == 12 ? 701 : 0;
    }

    public static int AllCodeUnits()
    {
        char[] data = new char[1];
        Span<char> writable = new Span<char>(data);
        ReadOnlySpan<char> view = writable;
        for (int i = 0; i <= char.MaxValue; i++)
        {
            char codeUnit = (char)i;
            data[0] = codeUnit;
            if (Search(view, codeUnit) != 0 || Search(view, (char)(i ^ 1)) != -1) return 0;
        }
        return 709;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int Search(ReadOnlySpan<char> view, char needle) => view.IndexOf(needle);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static ReadOnlySpan<char> EmptyView()
    {
        evaluated = evaluated * 10 + 1;
        return default;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static char Needle()
    {
        evaluated = evaluated * 10 + 2;
        return '~';
    }
}
