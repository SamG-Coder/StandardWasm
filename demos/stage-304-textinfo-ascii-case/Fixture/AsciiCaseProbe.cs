public static class AsciiCaseProbe
{
    public static string Input(int index)
    {
        switch (index)
        {
            case 0: return "";
            case 1: return "a-_0";
            case 2: return "@ABC[";
            case 3: return "aZA";
            case 4: return "ÉÜé😀AZ";
            case 5: return "KİΣ!";
            case 6: return "already Lower With Capitals";
            case 7: return "\u0000Z\ud800-\udfff\uffff";
            default: return null;
        }
    }

    public static int CodeUnit(string value, int index) => index < 0 ? value.Length : value[index];
}
