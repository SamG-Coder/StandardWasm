using System;

public static class ScopedSpanProbe
{
    private readonly ref struct CharacterView
    {
        internal readonly Span<char> Characters;

        internal CharacterView(Span<char> characters) => Characters = characters;
    }

    private ref struct MutableCharacterView
    {
        internal Span<char> Characters;
        internal int Count;

        internal MutableCharacterView(Span<char> characters, int count)
        {
            Characters = characters;
            Count = count;
        }
    }

    public static int CheckScopedReference(int seed)
    {
        var left = new char[2];
        var right = new char[2];
        left[1] = 'a';
        var view = new MutableCharacterView(new Span<char>(left), seed);
        var copy = view;
        ReplaceView(ref view, new Span<char>(right));
        var before = copy.Characters;
        var after = view.Characters;
        return before[1] + after[1] + right[1] + copy.Count + view.Count;
    }

    private static void ReplaceView(ref MutableCharacterView view, Span<char> replacement)
    {
        view.Count++;
        view.Characters = replacement;
        Span<char> alias = view.Characters;
        alias[1] = (char)view.Count;
    }

    public static unsafe int CheckStackBackedSpan(int scenario)
    {
        char* storage = stackalloc char[3];
        storage[0] = 'a';
        storage[1] = 'b';
        storage[2] = '\u2000';
        try
        {
            int length = scenario == 1 ? -1 : scenario == 4 ? 0 : 3;
            return ReadAndUpdate(new Span<char>(storage, length), storage, scenario);
        }
        catch (ArgumentOutOfRangeException) { return -101; }
        catch (IndexOutOfRangeException) { return -102; }
    }

    private static unsafe int ReadAndUpdate(Span<char> span, char* storage, int scenario)
    {
        var scope = new CharacterView(span);
        Span<char> writable = scope.Characters;
        writable[1] = '\ud800';
        ReadOnlySpan<char> readOnly = writable;
        int index = scenario == 2 ? -1 : scenario == 3 ? 3 : scenario == 5 ? 2 : 1;
        return readOnly[index] + storage[1] + writable[0] + writable.Length;
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
