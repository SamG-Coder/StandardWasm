using System;

namespace ProvenStringSpanMoves
{
    public static class Probe
    {
        private const string Text = "A\0Ω🙂Z";

        public static int ExactSlice()
        {
            char[] target = Make();
            bool copied = Text.TryCopyTo(Interior(target, Text.Length));
            return copied && ContentsMatch(target) ? 1 : 0;
        }

        public static int TooShort()
        {
            char[] target = Make();
            bool copied = Text.TryCopyTo(Interior(target, Text.Length - 1));
            return !copied && Unchanged(target) ? 1 : 0;
        }

        public static int LargerSlice()
        {
            char[] target = Make();
            Span<char> destination = Interior(target, Text.Length + 2);
            Span<char> alias = destination;
            bool copied = Text.TryCopyTo(destination);
            return copied && ContentsMatch(target) && alias[2] == 'Ω' ? 1 : 0;
        }

        public static int EmptyDefault() => string.Empty.TryCopyTo(default) && !Text.TryCopyTo(default) ? 1 : 0;

        public static int CopyToExact()
        {
            char[] target = Make();
            Text.CopyTo(Interior(target, Text.Length));
            return ContentsMatch(target) ? 1 : 0;
        }

        public static int CopyToShort()
        {
            char[] target = Make();
            try
            {
                Text.CopyTo(Interior(target, Text.Length - 1));
                return 0;
            }
            catch (ArgumentException)
            {
                return Unchanged(target) ? 1 : 0;
            }
        }

        private static bool ContentsMatch(char[] target) => target[0] == '#' && target[1] == 'A' &&
            target[2] == '\0' && target[3] == 'Ω' && target[4] == '\ud83d' && target[5] == '\ude42' &&
            target[6] == 'Z' && target[7] == '#' && target[8] == '#';

        private static bool Unchanged(char[] target)
        {
            for (int i = 0; i < target.Length; i++) if (target[i] != '#') return false;
            return true;
        }

        private static Span<char> Interior(char[] target, int length)
        {
            Span<char> view = new Span<char>(target);
            return view.Slice(1, length);
        }

        private static char[] Make()
        {
            var chars = new char[10];
            for (int i = 0; i < chars.Length; i++) chars[i] = '#';
            return chars;
        }
    }
}
