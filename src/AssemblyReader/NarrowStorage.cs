namespace PeLibrary
{
    internal static class NarrowStorage
    {
        // Values above the byte range describe storage, not evaluation stack
        // types. All five cells load to the CLI int32 stack category.
        internal const int Signed8 = 256, Unsigned8 = 257, Signed16 = 258, Unsigned16 = 259, Character = 260;

        internal static int FromElement(int element)
        {
            if (element == 2) return Unsigned8; // Boolean storage uses one unsigned byte.
            if (element == 3) return Character;
            return element >= 4 && element <= 7 ? Signed8 + element - 4 : 0;
        }
    }
}
