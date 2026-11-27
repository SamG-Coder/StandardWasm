using System;
using System.Runtime.CompilerServices;

namespace CharacterReadOnlySpansFixture;

[InlineArray(3)]
public struct Slots
{
    private object first;
}

public static class Kernels
{
    private const string Text = "A\u00e9\ud83d\ude00\uffff\0";

    private static int Read(ReadOnlySpan<char> view, int index) => view[index];
    private static int Length(ReadOnlySpan<char> view) => view.Length;

    public static int CodeUnit(int index) => Read(Text.AsSpan(), index);

    public static int SelectedLength(int kind)
    {
        string text;
        if (kind == 0) text = null;
        else if (kind == 1) text = "";
        else text = Text;
        return Length(text.AsSpan());
    }

    public static int CheckedIndex(int index, int useNull)
    {
        string text = useNull == 0 ? Text : null;
        try { return Read(text.AsSpan(), index); }
        catch (IndexOutOfRangeException) { return -1; }
    }

    private static int ReadBoth(ReadOnlySpan<char> text, ReadOnlySpan<object> objects, int index)
    {
        if (objects[index] == null) return text.Length;
        return text[index];
    }

    public static int MixedKinds(int index)
    {
        Slots slots = default;
        slots[1] = new object();
        return ReadBoth(Text.AsSpan(), slots, index);
    }
}

// A separate export lets the integration probe link the real CoreLib overload
// with the same char and object span signatures used by SR.Format.
public static class CoreLibProbe
{
    public static int JoinOriginal()
    {
        Slots slots = default;
        slots[1] = "framework";
        return string.Join("-", (ReadOnlySpan<object>)slots).Length;
    }
}

public static class CrossAssemblyProbe
{
    public static int CallCompanion()
    {
        Slots slots = default;
        slots[1] = new object();
        return SpanCompanion.Readers.Combine("Aé".AsSpan(), slots, 1);
    }
}
