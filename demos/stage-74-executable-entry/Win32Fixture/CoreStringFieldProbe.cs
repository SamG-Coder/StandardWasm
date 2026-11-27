using System;

public static class CoreStringFieldProbe
{
    public static int CheckCoreStringFirstCharacter(int mode)
    {
        string value = mode == 0 ? "" : mode == 1 ? "abc" : mode == 2 ? "\u2000x" :
            mode == 3 ? null : "\0x";
        char first = mode == 2 ? '\u2000' : mode == 4 ? '\0' : 'a';
        try { return value.StartsWith(first) ? 1 : 0; }
        catch (NullReferenceException) { return -1; }
    }
}
