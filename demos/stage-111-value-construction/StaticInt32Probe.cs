namespace ValueConstructionDemo
{
    public static class StaticInt32Probe
    {
        private static int cached;
        private static int other;

        static StaticInt32Probe()
        {
            cached = 17;
            other = 31;
        }

        private static int Update(ref int first, ref int second, int delta)
        {
            first = unchecked(first + delta);
            int observed = second;
            second = unchecked(second + 1);
            return observed;
        }

        private static ref int GetCached() => ref cached;

        private static ref int Forward(ref int value) => ref value;

        private static class PreciseSlot<T>
        {
            internal static readonly int Initial;
            internal static int Current;

            static PreciseSlot()
            {
                Current = 31;
                Initial = ++Current;
            }

            internal static int Observe() => Current + Initial;
        }

        private static class DeferredSlot<T>
        {
            internal static int Count = 7;
            internal static T Value;
        }

        public static int CheckClosedGenericStatics(int input)
        {
            int initial = PreciseSlot<int>.Observe();
            PreciseSlot<int>.Current = unchecked(initial + input);
            int independent = PreciseSlot<string>.Observe();
            DeferredSlot<int>.Count++;
            DeferredSlot<int>.Value = input;
            DeferredSlot<string>.Value = "abc";
            return unchecked(PreciseSlot<int>.Observe() * 10000 + independent * 100 +
                DeferredSlot<int>.Count * 10 + DeferredSlot<string>.Count +
                DeferredSlot<int>.Value + DeferredSlot<string>.Value.Length);
        }

        public static int CheckFrameInt32Aliases(int input)
        {
            int local = unchecked(input * 3);
            ref int alias = ref Forward(ref local);
            int same = Update(ref local, ref alias, 2);
            int distinct = Update(ref input, ref alias, 5);
            return unchecked(same * 1000 + distinct * 100 + local * 10 + input);
        }

        public static int CheckStaticInt32Aliases(int delta)
        {
            cached = unchecked(cached + delta);
            int same = Update(ref GetCached(), ref cached, 2);
            int distinct = Update(ref cached, ref other, 3);
            return unchecked(same * 1000 + distinct * 100 + cached * 10 + other);
        }
    }
}
