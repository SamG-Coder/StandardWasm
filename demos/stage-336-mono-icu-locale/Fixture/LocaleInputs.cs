public static class LocaleInputs
{
    public static string Input(int index)
    {
        switch (index)
        {
            case 0: return "";
            case 1: return "en";
            case 2: return "en-US";
            case 3: return "en-XX";
            case 4: return "en_US";
            case 5: return "fr-FR";
            case 6: return "zh-Hant-TW";
            case 7: return "iw-IL";
            case 8: return "qaa-Qaaa-XX";
            case 9: return "en-US-u-co-phonebk";
            case 10: return "en/US";
            case 11: return "x-foo";
            case 12: return "\u00e9";
            case 13: return "root";
            case 14: return "en\0fast";
            case 15: return "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
            default: return null;
        }
    }
}
