namespace ForeignCounter
{
    public sealed class Counter
    {
        private int value;

        public Counter(int seed) => value = seed;
        public Counter(uint seed) => value = unchecked((int)seed + 100);
        public Counter(long seed) => value = checked((int)seed);

        public Counter Shift(int delta)
        {
            value = unchecked(value + delta);
            return this;
        }

        public int Shift(uint delta) => unchecked(value - (int)delta);
        public int Read() => value;
    }

    public class DynamicCounter
    {
        public virtual int Read() => 7;
    }
}
