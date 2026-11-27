using System;

public static unsafe class NativeStringProbe
{
    public static int Snapshot()
    {
        char* units = stackalloc char[5];
        units[0] = 'p';
        units[1] = '\ud801';
        units[2] = '\0';
        units[3] = '\udc00';
        units[4] = 'q';
        string result = new string(new Span<char>(units + 1, 3));
        units[1] = 'x';
        return result[0] + result[1] + result[2] + result.Length;
    }

    public static int ZeroLength()
    {
        string result = new string(new Span<char>((void*)0, 0));
        return object.ReferenceEquals(result, string.Empty) ? 1 : 0;
    }

    public static int DirectConstructor()
    {
        char* units = stackalloc char[4];
        units[0] = 'p';
        units[1] = '\u03a9';
        units[2] = '\0';
        units[3] = 'q';
        string result = new string(units, 1, 2);
        units[1] = 'x';
        return result[0] + result[1] + result.Length;
    }

    public static void CheckClrGuards()
    {
        CheckRejected(-1, -1, "length");
        CheckRejected(-1, 0, "startIndex");
        CheckRejected(0, 1, "ptr");
        if (!object.ReferenceEquals(new string((char*)0, 0, 0), string.Empty))
            throw new InvalidOperationException("Expected the canonical empty string.");
    }

    private static void CheckRejected(int start, int length, string parameter)
    {
        try { _ = new string((char*)0, start, length); }
        catch (ArgumentOutOfRangeException exception) when (exception.ParamName == parameter) { return; }
        throw new InvalidOperationException("Expected the original pointer string guard.");
    }

    // Only invoked inside the guest Wasm memory boundary.
    public static int OutsideWasmMemory() =>
        new string(new Span<char>((void*)(nuint)0xfffffffeu, 2)).Length;
}
