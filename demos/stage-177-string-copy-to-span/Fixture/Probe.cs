using System;

namespace StringCopyToSpan;

public static unsafe class Probe
{
    public static int Slice()
    {
        char[] letters = ['l', 'x', 'x', 'x', 'r'];
        var output = new Span<char>(letters);
        "Ω😀".CopyTo(output.Slice(1, 3));
        return letters[0] == 'l' && letters[1] == 'Ω' && letters[2] == '\ud83d' &&
            letters[3] == '\ude00' && letters[4] == 'r' ? 433 : 0;
    }

    public static int NativeDestination()
    {
        char* data = stackalloc char[3];
        data[2] = 's';
        var output = new Span<char>(data, 3);
        "ab".CopyTo(output);
        return data[0] == 'a' && data[1] == 'b' && data[2] == 's' ? 439 : 0;
    }

    public static int ShortDestination()
    {
        char[] data = ['x'];
        var output = new Span<char>(data);
        try { "ab".CopyTo(output); }
        catch (ArgumentException) { return data[0] == 'x' ? 443 : 0; }
        return 0;
    }

    public static int Empty()
    {
        Span<char> output = default;
        "".CopyTo(output);
        return output.Length == 0 ? 449 : 0;
    }

    public static int NullSource()
    {
        string source = null;
        Span<char> output = default;
        try { source.CopyTo(output); }
        catch (NullReferenceException) { return 457; }
        return 0;
    }
}
