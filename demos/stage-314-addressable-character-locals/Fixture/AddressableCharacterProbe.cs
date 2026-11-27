using System;

public static class AddressableCharacterProbe
{
    private static char ReadAt(ReadOnlySpan<char> values, int index) => values[index];

    private static bool WriteRange(ReadOnlySpan<char> values, out char first, out char last)
    {
        int length = values.Length;
        if (length == 0)
        {
            first = '\0';
            last = '\u8000';
            return false;
        }
        first = ReadAt(values, 0);
        last = ReadAt(values, length - 1);
        return true;
    }

    private static void Set(out char value) => value = '\ufffe';
    private static void ForwardValue(out char value) => Set(out value);

    public static int Pair()
    {
        WriteRange("Ω\uffff".AsSpan(), out char first, out char last);
        return first * 256 + last;
    }

    public static int Empty()
    {
        bool found = WriteRange(default, out char first, out char last);
        return found ? -1 : first + last;
    }

    public static int Alias()
    {
        WriteRange("ABC".AsSpan(), out char value, out value);
        return value;
    }

    public static int Forward()
    {
        ForwardValue(out char value);
        return value;
    }

    public static int AfterStore()
    {
        Set(out char value);
        value = 'Ω';
        return value;
    }

    private static ReadOnlySpan<char> KeepInput(ReadOnlySpan<char> input)
    {
        Set(out char scratch);
        return input;
    }

    public static int RejectedLifetime()
    {
        var view = KeepInput("A".AsSpan());
        return view.Length;
    }
}
