namespace Sample
{
    // A checked increment must raise managed overflow at the upper bound.
    public static class CheckedArithmetic
    {
        public static int Increment(int value) => checked(value + 1);
    }
}
