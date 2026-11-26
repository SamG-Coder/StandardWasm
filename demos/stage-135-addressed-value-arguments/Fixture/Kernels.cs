namespace AddressedValueFixture
{
    public struct Pair
    {
        public int First;
        public int Second;

        public Pair(int first, int second)
        {
            First = first;
            Second = second;
        }

        public int Combine(int multiplier) => unchecked(First * multiplier + Second);
    }

    public static class Kernels
    {
        public static int Read(int first, int second) => ReadArgument(new Pair(first, second), 31);

        public static int MutateCopy(int first, int second)
        {
            var value = new Pair(first, second);
            int changed = ChangeArgument(value, -23);
            return unchecked(changed * 7 + value.Combine(31));
        }

        public static int ClearCopy(int first, int second)
        {
            var value = new Pair(first, second);
            return ClearArgument(value) + value.Combine(31);
        }

        private static int ReadArgument(Pair value, int multiplier) => value.Combine(multiplier);

        private static int ChangeArgument(Pair value, int replacement)
        {
            value.First = replacement;
            return value.Combine(31);
        }

        private static int ClearArgument(Pair value)
        {
            value = default;
            return value.Combine(31);
        }
    }
}
