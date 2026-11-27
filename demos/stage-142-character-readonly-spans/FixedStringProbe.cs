using System;

namespace CharacterReadOnlySpansFixture
{
    public static class FixedStringProbe
    {
        public static int Read(int selection)
        {
            string value = selection == 0 ? null : selection == 1 ? "" :
                selection == 2 ? "\u6f22" : selection == 3 ? "\U0001f642" : "\0X";
            try { return FirstCodeUnit(value); }
            catch (NullReferenceException) { return -1; }
        }

        private static unsafe int FirstCodeUnit(string value)
        {
            fixed (char* pointer = value)
                return *pointer;
        }
    }
}
