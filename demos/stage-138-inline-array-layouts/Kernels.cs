using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace InlineArrayLayouts;

[InlineArray(3)]
public struct Words
{
    public int First;
}

[InlineArray(3)]
[StructLayout(LayoutKind.Auto)]
public struct Slots<T>
{
    private T _first;
}

public static class Kernels
{
    public static int CopyFirst(int initial, int replacement)
    {
        Words original = default;
        original.First = initial;
        Words copy = original;
        original.First = replacement;
        return unchecked(copy.First - original.First);
    }

    private static int Read(Words value) => value.First;
    private static int ReadSlots(Slots<object> value, int marker) => marker;

    public static int PassByValue(int value)
    {
        Words words = default;
        words.First = value;
        return Read(words);
    }

    public static int DefaultFirst() => Read(default);

    public static int GenericReferenceSlots(int marker)
    {
        Slots<object> slots = default;
        return ReadSlots(slots, marker);
    }
}

[InlineArray(10001)]
public struct WideWords
{
    public int First;
}

public static class LayoutBoundary
{
    public static int Read(int seed)
    {
        WideWords value = default;
        value.First = seed;
        return value.First;
    }
}
