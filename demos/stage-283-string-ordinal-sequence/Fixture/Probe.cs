namespace StringOrdinalSequence
{
    public static class Probe
    {
        // Keep the original op_Inequality MemberRef in a distinct method so
        // constant strings at the call sites cannot erase the linked operation.
        private static bool Different(string left, string right) => left != right;

        public static int DistinctEqualStrings()
        {
            string left = new string(new[] { 'A', '漢' });
            string right = new string(new[] { 'A', '漢' });
            return !object.ReferenceEquals(left, right) && !Different(left, right) ? 101 : -1;
        }

        public static int BothBytesOfCodeUnit() =>
            Different("x\u0101", "x\u0201") && Different("x\u0101", "x\u0102") ? 103 : -1;

        public static int EmbeddedNull() => Different("a\0b", "a\0c") && !Different("a\0", "a\0") ? 107 : -1;

        public static int UnpairedSurrogates()
        {
            string left = new string(new[] { '\ud800' });
            string copy = new string(new[] { '\ud800' });
            string other = new string(new[] { '\ud801' });
            return !Different(left, copy) && Different(left, other) ? 109 : -1;
        }

        public static int ReferenceAndNull() =>
            !Different("same", "same") && !Different(null, null) && Different(null, "x") && Different("x", null) ? 113 : -1;

        public static int EmptyAndUnequalLengths() =>
            !Different("", "") && Different("", "a") && Different("abc", "a") && Different("a", "abc") ? 127 : -1;

        public static int OrdinaryHelper() => LocalStringAlgorithm.EqualsHelper("a", "b") ? 131 : -1;
    }

    internal static class LocalStringAlgorithm
    {
        internal static bool EqualsHelper(string left, string right) => left[0] < right[0];
    }
}
