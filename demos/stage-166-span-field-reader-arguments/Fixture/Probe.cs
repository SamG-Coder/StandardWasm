using System;
using System.Runtime.CompilerServices;

namespace SpanFieldReaderArguments;

public ref struct Buffer
{
    private Span<char> data;
    private int position;

    public Buffer(char[] data, int position)
    {
        this.data = new Span<char>(data);
        this.position = position;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private int ReadPosition() => position;

    public int Write(char value)
    {
        data[ReadPosition()] = value;
        return data[ReadPosition()];
    }

    public ReadOnlySpan<char> SliceAt(int length) => data.Slice(ReadPosition(), length);

    public ReadOnlySpan<char> SliceFromStart() => data.Slice(0, ReadPosition());
}

public ref struct ReplacingBuffer
{
    private Span<char> data;
    private Span<char> replacement;

    public ReplacingBuffer(char[] data, char[] replacement)
    {
        this.data = new Span<char>(data);
        this.replacement = new Span<char>(replacement);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private int ReplaceAndReadPosition()
    {
        data = replacement;
        return 1;
    }

    public void Write()
    {
        data[ReplaceAndReadPosition()] = 'Y';
    }
}

public static class Probe
{
    public static int IndexThroughFieldReader()
    {
        char[] original = ['a', 'b', 'c'];
        var buffer = new Buffer(original, 1);
        return buffer.Write('Z') == 'Z' && original[0] == 'a' &&
            original[1] == 'Z' && original[2] == 'c' ? 241 : 0;
    }

    public static int SliceThroughFieldReader()
    {
        char[] original = ['m', 'n', 'o', 'p'];
        var buffer = new Buffer(original, 2);
        ReadOnlySpan<char> suffix = buffer.SliceAt(2);
        ReadOnlySpan<char> prefix = buffer.SliceFromStart();
        original[2] = 'Q';
        return prefix.Length == 2 && prefix[1] == 'n' &&
            suffix.Length == 2 && suffix[0] == 'Q' && suffix[1] == 'p' ? 251 : 0;
    }

    public static int OutOfRangeThroughFieldReader()
    {
        char[] original = ['a'];
        var buffer = new Buffer(original, 1);
        try { return buffer.Write('Z'); }
        catch (IndexOutOfRangeException) { return original[0] == 'a' ? 257 : 0; }
    }

    public static int MutatingArgumentMustStayDiagnosed()
    {
        char[] original = ['a', 'b'];
        char[] replacement = ['c', 'd'];
        var buffer = new ReplacingBuffer(original, replacement);
        buffer.Write();
        // The managed address refers to the descriptor after the argument
        // replaces it. An eager snapshot would write into the wrong array.
        return original[1] == 'b' && replacement[1] == 'Y' ? 263 : 0;
    }
}
