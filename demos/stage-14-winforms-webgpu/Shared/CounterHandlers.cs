namespace CounterExample
{
    public static class CounterHandlers
    {
        public static int Advance(int count) => unchecked(count + 1);
    }
}
