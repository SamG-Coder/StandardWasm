namespace Sample
{
    // Negative fixture. A checked increment must never be lowered to wrapping add.
    public static class CheckedArithmetic
    {
        public static int Increment(int value) => checked(value + 1);
    }
}
