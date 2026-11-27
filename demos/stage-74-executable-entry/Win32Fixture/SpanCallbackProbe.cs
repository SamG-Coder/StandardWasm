using System;
using System.Buffers;

public static class SpanCallbackProbe
{
    private static int trace;

    public static int CheckStaticEventCallback(int mode)
    {
        trace = 0;
        EventHandler action = new EventHandler(First);
        action += new EventHandler(Second);
        if (mode == 1) action -= new EventHandler(First);
        if (mode == 2) action -= new EventHandler(Second);
        action(null, EventArgs.Empty);
        return trace;
    }

    private static void First(object sender, EventArgs args) => trace = trace * 10 + 1;
    private static void Second(object sender, EventArgs args) => trace = trace * 10 + 2;

    public static int CheckCharacterCallback(int scenario)
    {
        char[] text = new char[2];
        SpanAction<char, nint> action = scenario < 0 ? null :
            scenario == 0 ? new SpanAction<char, nint>(WriteStatic) :
            new SpanAction<char, nint>(new Writer(scenario * 10).WriteInstance);
        try
        {
            action(new Span<char>(text), (nint)(scenario + 1));
            return text[0] * 100 + text[1];
        }
        catch (NullReferenceException)
        {
            return -1;
        }
    }

    private static void WriteStatic(Span<char> text, nint state)
    {
        text[0] = (char)state;
        text[1] = 's';
    }

    private sealed class Writer
    {
        private readonly int prefix;
        internal Writer(int prefix) => this.prefix = prefix;

        internal void WriteInstance(Span<char> text, nint state)
        {
            text[0] = (char)(prefix + (int)state);
            text[1] = 'i';
        }
    }
}
