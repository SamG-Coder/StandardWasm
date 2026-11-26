using System;

namespace SpanCompanion;

public static class Readers
{
    public static int Combine(ReadOnlySpan<char> text, ReadOnlySpan<object> items, int index)
        => items[index] == null ? -1 : text[index] + items.Length;

    public static int Combine(ReadOnlySpan<object> items, ReadOnlySpan<char> text, int index)
        => -2;
}
