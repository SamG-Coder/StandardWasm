using System;

public static class Probe
{
    public static int Aliases()
    {
        byte[] storage = new byte[4];
        storage[0] = 13;
        storage[3] = 17;
        Span<byte> view = storage.AsSpan(1, 2);
        Span<byte> slice = view.Slice(1, 1);
        Write(view);
        slice[0] = 251;
        return view[0] == 193 && view[1] == 251 && storage[1] == 193 &&
            storage[2] == 251 && storage[0] == 13 && storage[3] == 17 ? 101 : 0;
    }

    public static int ReadOnlyAlias()
    {
        byte[] storage = new byte[3];
        Span<byte> writable = storage.AsSpan(1, 2);
        ReadOnlySpan<byte> readOnly = writable;
        writable[0] = 221;
        storage[2] = 173;
        return readOnly[0] == 221 && readOnly[1] == 173 && readOnly.Length == 2 ? 103 : 0;
    }

    public static int EmptyRanges()
    {
        Span<byte> none = MemoryExtensions.AsSpan<byte>(null, 0, 0);
        Span<byte> end = Make(new byte[2], 2, 0);
        return none.IsEmpty && end.Length == 0 ? 107 : 0;
    }

    public static int InvalidRanges() =>
        Reject(new byte[2], -1, 0) && Reject(new byte[2], 0, -1) &&
        Reject(new byte[2], 1, int.MaxValue) && Reject(null, 0, 1) &&
        Reject(null, 1, 0) ? 109 : 0;

    public static int SpanBounds()
    {
        Span<byte> view = new byte[3].AsSpan(1, 1);
        try
        {
            view[1] = 99;
            return 0;
        }
        catch (IndexOutOfRangeException)
        {
            return 113;
        }
    }

    private static Span<byte> Make(byte[] array, int start, int length) => new Span<byte>(array, start, length);

    private static bool Reject(byte[] bytes, int start, int length)
    {
        try
        {
            Span<byte> view = bytes.AsSpan(start, length);
            return false;
        }
        catch (ArgumentOutOfRangeException)
        {
            return true;
        }
    }

    private static void Write(Span<byte> view) => view[0] = 193;
}
