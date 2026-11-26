using System;

namespace CounterExample
{
    public sealed class CounterState
    {
        private int count;

        public CounterState(int initial) => count = initial;

        public int Advance()
        {
            count = unchecked(count + 1);
            return count;
        }

        public int Value() => count;
    }

    public static class CounterHandlers
    {
        public static CounterState Create(int initial) => new CounterState(initial);
        public static Func<int> Bind(CounterState state) => state.Advance;
        public static int Dispatch(Func<int> callback) => callback();
        public static int Read(CounterState state) => state.Value();
    }
}
