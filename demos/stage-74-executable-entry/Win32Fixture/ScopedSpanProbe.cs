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

        internal void Reset() => this = default;
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

    public static int CheckScopedReceiverReset(int seed)
    {
        var owner = new char[2];
        owner[0] = (char)seed;
        var view = new MutableCharacterView(new Span<char>(owner), seed);
        var snapshot = view;
        var direct = view;
        ResetView(ref view);
        direct.Reset();
        Span<char> cleared = view.Characters;
        Span<char> clearedDirect = direct.Characters;
        Span<char> prior = snapshot.Characters;
        return view.Count + cleared.Length + direct.Count + clearedDirect.Length +
            snapshot.Count + prior.Length + prior[0];
    }

    private static void ResetView(ref MutableCharacterView view) => view.Reset();

    private readonly struct ScopeState
    {
        internal readonly int Value;

        internal ScopeState(int value) => Value = value;
    }

    private readonly ref struct NestedScope
    {
        internal readonly ScopeState State;

        internal NestedScope(ScopeState state) => State = state;
    }

    public static int CheckScopedNestedValue(int seed)
    {
        var scope = new NestedScope(new ScopeState(seed));
        ScopeState original = scope.State;
        scope = new NestedScope(new ScopeState(unchecked(seed + 1)));
        ScopeState replacement = scope.State;
        return original.Value ^ replacement.Value;
    }

    public static unsafe int CheckSpanFromEnd(int length)
    {
        if (length < 0 || length > 3) return -101;
        char* storage = stackalloc char[3];
        storage[0] = 'a';
        storage[1] = '\u2000';
        storage[2] = '\ud800';
        ReadOnlySpan<char> view = new Span<char>(storage, length);
        try { return ReadLast(view); }
        catch (IndexOutOfRangeException) { return -102; }
    }

    private static int ReadLast(ReadOnlySpan<char> view) => view[^1];

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
