using System.IO;

public static class Guest
{
    public static int PathCharacters(int index)
    {
        char[] values = Path.GetInvalidPathChars();
        return index < 0 ? values.Length : values[index];
    }

    public static int FileCharacters(int index)
    {
        char[] values = Path.GetInvalidFileNameChars();
        return index < 0 ? values.Length : values[index];
    }

    public static int FreshPathArray(int replacement)
    {
        char[] first = Path.GetInvalidPathChars();
        char[] second = Path.GetInvalidPathChars();
        int original = second[0];
        first[0] = (char)replacement;
        return original == second[0] && first[0] == (char)replacement ? 1 : 0;
    }

    public static int LibraryLiteral(int index)
    {
        char[] values = { '\0', '\u00ff', '\u0100', '\u1234', '\u8000', '\uffff' };
        return index < 0 ? values.Length : values[index];
    }
}
