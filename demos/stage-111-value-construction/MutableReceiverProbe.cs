using System;
using System.Collections.Specialized;

namespace ValueConstructionDemo
{
    public static class MutableReceiverProbe
    {
        private struct Counter
        {
            public int Value;

            public int Add(int delta)
            {
                Value = unchecked(Value + delta);
                return Value;
            }

            public int AddWithAlias(ref Counter alias, int delta)
            {
                Add(delta);
                return alias.Value;
            }

            public int WriteThenCheck(int value)
            {
                Value = value;
                return checked(value - int.MinValue);
            }
        }

        private static int UpdateCopy(Counter counter) => counter.Add(7);
        private static int ReadValue(Counter counter) => counter.Value;

        public static int CheckMutableReceiver(int input)
        {
            var counter = new Counter { Value = input };
            int current = counter.Add(3);
            int copy = UpdateCopy(counter);
            int viaAlias = counter.AddWithAlias(ref counter, 5);
            return unchecked(current * 3 + copy * 5 + viaAlias * 7 + ReadValue(counter));
        }

        public static int CheckExceptionalMutation(int input)
        {
            var counters = new Counter[1];
            ref Counter alias = ref counters[0];
            try { return counters[0].WriteThenCheck(input); }
            catch (OverflowException) { return unchecked(alias.Value * 3 + 19); }
        }

        public static int CheckClosedReceiver(int input)
        {
            int? value = input == 0 ? (int?)null : input;
            return value.HasValue ? 17 : 23;
        }

        public static int CheckFrameworkReceiver(int input)
        {
            var vector = new BitVector32(input);
            vector[1] = true;
            vector[2] = false;
            return vector.Data;
        }
    }
}
