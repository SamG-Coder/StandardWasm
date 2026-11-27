using System;

public static class ScopedSpanProbe
{
    private readonly ref struct CharacterView
    {
        internal readonly Span<char> Characters;

        internal CharacterView(Span<char> characters) => Characters = characters;
    }

    public static int ScopedWritableSpan(int value)
    {
        var owner = new char[2];
        owner[0] = (char)value;
        var scope = new CharacterView(new Span<char>(owner));
        var copy = scope;
        Span<char> alias = copy.Characters;
        alias[1] = (char)(value + 1);
        CharacterView zero = default;
        Span<char> empty = zero.Characters;
        return alias[0] + owner[1] + empty.Length;
    }
}
