namespace NativeUtf16Fixture
{
    public static unsafe class Probe
    {
        public static int NullPointer() => ReferenceEquals(new string((char*)0), string.Empty) ? 1 : 0;

        public static int EmptyBuffer()
        {
            char* input = stackalloc char[2];
            input[0] = '\0';
            input[1] = 'X';
            return ReferenceEquals(new string(input), string.Empty) ? 1 : 0;
        }

        public static int UnalignedAndEmbeddedNul()
        {
            byte* bytes = stackalloc byte[17];
            char* input = (char*)(bytes + 1);
            input[0] = 'é';
            input[1] = '\ud83d';
            input[2] = '\ude42';
            input[3] = '\ud800';
            input[4] = '\0';
            input[5] = 'X';
            string result = new string(input);
            input[0] = 'z';
            return result.Length == 4 && result[0] == 'é' && result[1] == '\ud83d' &&
                result[2] == '\ude42' && result[3] == '\ud800' ? 1 : 0;
        }

        public static int OwnsCopy()
        {
            char* input = stackalloc char[3];
            input[0] = 'K';
            input[1] = '9';
            input[2] = '\0';
            string first = new string(input);
            string second = new string(input);
            input[0] = 'X';
            return !ReferenceEquals(first, second) && first.Length == 2 && first[0] == 'K' &&
                second[1] == '9' ? 1 : 0;
        }
    }
}
