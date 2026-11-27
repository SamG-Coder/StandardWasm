using System;

namespace CharacterSpanCopy;

public static unsafe class Probe
{
    public static int OriginalConcat()
    {
        string value = string.Concat("π".AsSpan(), "0123".AsSpan().Slice(1, 2), "Z".AsSpan());
        return value.Length == 4 && value[0] == 'π' && value[1] == '1' && value[2] == '2' && value[3] == 'Z' ? 193 : 0;
    }

    public static int OriginalPairConcat()
    {
        string value = string.Concat("α".AsSpan(), "βγ".AsSpan());
        return value.Length == 3 && value[0] == 'α' && value[1] == 'β' && value[2] == 'γ' ? 191 : 0;
    }

    public static int EmptySource()
    {
        ReadOnlySpan<char> empty = default;
        Span<char> destination = default;
        empty.CopyTo(destination);
        return string.Concat(empty, empty, empty).Length;
    }

    public static int ArrayOverlap()
    {
        char[] data = ['a', 'b', 'c', 'd'];
        var span = new Span<char>(data);
        ReadOnlySpan<char> source = span.Slice(0, 3);
        var destination = span.Slice(1, 3);
        source.CopyTo(destination);
        return data[0] == 'a' && data[1] == 'a' && data[2] == 'b' && data[3] == 'c' ? 197 : 0;
    }

    public static int NativeOverlap()
    {
        char* data = stackalloc char[4];
        data[0] = 'a';
        data[1] = 'b';
        data[2] = 'c';
        data[3] = 'd';
        var span = new Span<char>(data, 4);
        ReadOnlySpan<char> source = span.Slice(0, 3);
        var destination = span.Slice(1, 3);
        source.CopyTo(destination);
        return data[0] == 'a' && data[1] == 'a' && data[2] == 'b' && data[3] == 'c' ? 199 : 0;
    }

    public static int Mixed()
    {
        char* storage = stackalloc char[2];
        storage[0] = 'x';
        storage[1] = 'y';
        char[] data = ['a', 'b'];
        var native = new Span<char>(storage, 2);
        var managed = new Span<char>(data);
        ReadOnlySpan<char> nativeSource = native;
        nativeSource.CopyTo(managed);
        data[0] = 'z';
        ReadOnlySpan<char> managedSource = managed;
        managedSource.CopyTo(native);
        return storage[0] == 'z' && storage[1] == 'y' && data[1] == 'y' ? 211 : 0;
    }

    public static int ShortDestination()
    {
        char[] data = ['a', 'b'];
        var span = new Span<char>(data);
        ReadOnlySpan<char> source = span;
        var destination = span.Slice(1, 1);
        try { source.CopyTo(destination); }
        catch (ArgumentException) { return data[1] == 'b' ? 223 : 0; }
        return 0;
    }
}
