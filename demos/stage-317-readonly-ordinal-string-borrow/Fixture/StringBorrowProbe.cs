public static class StringBorrowProbe
{
    public static string Input(int index)
    {
        switch (index)
        {
            case 0: return "Omega";
            case 1: return "\u03a9!";
            case 2: return "\ud801\udc00";
            case 3: return "";
            default: return null;
        }
    }
}
