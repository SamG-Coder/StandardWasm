using System;

namespace WritableCharacterSpansFixture;

public static class Kernels
{
    private static void Assign(ref char target, int value) => target = unchecked((char)value);
    private static int Read(Span<char> span, int index) => span[index];
    private static int Read(ReadOnlySpan<char> span, int index) => span[index];

    public static int BufferAliasing(int index, int value)
    {
        var buffer = new char[3];
        Span<char> span = new Span<char>(buffer);
        ReadOnlySpan<char> view = span;
        ref char alias = ref span[index];
        Assign(ref alias, value);
        int observed = buffer[index];
        if (Read(span, index) != observed || Read(view, index) != observed) return -1;
        return observed;
    }

    public static int IndependentSlots(int index)
    {
        var buffer = new char[2];
        Span<char> first = new Span<char>(buffer);
        Span<char> second = first;
        first[0] = 'A';
        second[1] = 'B';
        return first[index] + buffer[1 - index];
    }

    public static int LengthOfEmpty(int fromNull)
    {
        Span<char> span = default;
        if (fromNull != 0)
        {
            char[] buffer = null;
            span = new Span<char>(buffer);
        }
        return span.Length;
    }

    private static char Mark(char[] trace)
    {
        trace[0] = 'X';
        return 'Z';
    }

    public static int CheckOrder(int index, int empty)
    {
        var trace = new char[1];
        char[] buffer = empty == 0 ? new char[2] : null;
        Span<char> span = new Span<char>(buffer);
        try { span[index] = Mark(trace); }
        catch (IndexOutOfRangeException) { return trace[0] == 0 ? 17 : -1; }
        return span[index] == 'Z' && trace[0] == 'X' ? 23 : -2;
    }

    private static void AssignThenDivide(ref char slot, int divisor)
    {
        slot = 'Y';
        if (10 / divisor == 1) slot = 'W';
    }

    public static int SurvivesUnwind(int divisor)
    {
        var buffer = new char[1];
        Span<char> span = new Span<char>(buffer);
        try { AssignThenDivide(ref span[0], divisor); }
        catch (DivideByZeroException) { return buffer[0]; }
        return Read(span, 0);
    }
}
