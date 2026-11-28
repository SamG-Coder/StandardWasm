namespace CoreLibConcatConstruction
{
    public static class Probe
    {
        public static int ThreeRawCodeUnitCopies()
        {
            var first = new string(new[] { '\u0101', '\0' });
            var second = new string(new[] { '\ud800', 'x' });
            var third = new string(new[] { '\udc00', '\u0202' });
            string result = string.Concat(first, second, third);
            return result.Length == 6 && result[0] == '\u0101' && result[1] == '\0' &&
                result[2] == '\ud800' && result[3] == 'x' && result[4] == '\udc00' && result[5] == '\u0202' ? 101 : -1;
        }

        public static int FourSeparateOffsets()
        {
            var first = new string(new[] { 'A' });
            var second = new string(new[] { 'b', 'c' });
            var third = new string(new[] { 'd', 'e', 'f' });
            var fourth = new string(new[] { 'g', 'h', 'i', 'j' });
            string result = string.Concat(first, second, third, fourth);
            return result == "Abcdefghij" && first == "A" && second == "bc" && third == "def" &&
                fourth == "ghij" ? 103 : -1;
        }

        public static int SoleSurvivorIdentity()
        {
            var original = new string(new[] { 'p', 'q' });
            string left = string.Concat(original, "", null);
            string middle = string.Concat("", original, null);
            string right = string.Concat(null, "", "", original);
            return ReferenceEquals(left, original) && ReferenceEquals(middle, original) &&
                ReferenceEquals(right, original) ? 107 : -1;
        }

        public static int CanonicalEmpty()
        {
            var emptyFromArray = new string(new char[0]);
            string empty = string.Concat(null, emptyFromArray, "", null);
            return ReferenceEquals(empty, string.Empty) ? 109 : -1;
        }

        public static int FreshResultIdentity()
        {
            var source = new string(new[] { 'm', 'a' });
            string result = string.Concat(source, "z", "e");
            return result == "maze" && !ReferenceEquals(result, "maze") && !ReferenceEquals(result, source)
                && source == "ma" ? 113 : -1;
        }

        public static int RepeatedInputAndFallback()
        {
            var source = new string(new[] { 'x', '\0', 'y' });
            string repeated = string.Concat(source, source, source);
            string fallback = string.Concat(source, "", null, source);
            return repeated == "x\0yx\0yx\0y" && fallback == "x\0yx\0y" && source == "x\0y" ? 127 : -1;
        }
    }
}
