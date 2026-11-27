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

    public static int CheckCoreStringTrim(int mode)
    {
        string input = mode == 0 ? "" : mode == 1 ? "abc" : mode == 2 ? "  a b\t" :
            mode == 3 ? "\u00a0x\u2000" : mode == 4 ? "\t \r\n" :
            mode == 5 ? " \ud83d\ude00\0x\t" : mode == 6 ? "left " : " right";
        string output = input.Trim();
        int checksum = output.Length;
        for (int i = 0; i < output.Length; i++)
            checksum = unchecked(checksum * 31 + output[i]);
        return unchecked(checksum * 4 + (ReferenceEquals(output, input) ? 1 : 0) +
            (ReferenceEquals(output, string.Empty) ? 2 : 0));
    }
}
