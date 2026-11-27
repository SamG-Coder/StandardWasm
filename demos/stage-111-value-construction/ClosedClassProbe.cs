using System;

namespace ValueConstructionDemo
{
    public static class ClosedClassProbe
    {
        public interface IScore<T>
        {
            int Score(T value);
        }

        private class Scorer<T> : IScore<T>
        {
            public virtual int Score(T value) => 17;
        }

        private sealed class SignedScorer : Scorer<int>
        {
            public override int Score(int value) => unchecked(value * 3 + 11);
        }

        private sealed class ClosedOverride<T> : Scorer<T>
        {
            public override int Score(T value) => 29;
        }

        private class Hidden<T> : Scorer<T>
        {
            public new virtual int Score(T value) => 31;
        }

        private sealed class HiddenOverride<T> : Hidden<T>
        {
            public override int Score(T value) => 37;
        }

        private static int CallSigned(Scorer<int> scorer, int value) => scorer.Score(value);
        private static int CallUnsigned(Scorer<uint> scorer, uint value) => scorer.Score(value);
        private static int CallHidden(Hidden<int> scorer, int value) => scorer.Score(value);
        private static int CallInterface(IScore<int> scorer, int value) => scorer.Score(value);

        public static int CheckClosedClassSlots(int value)
        {
            int signed = value < 0 ? CallThroughBoth(new SignedScorer(), value) : CallThroughBoth(new ClosedOverride<int>(), value);
            var hidden = new HiddenOverride<int>();
            return unchecked(signed + CallUnsigned(new ClosedOverride<uint>(), (uint)value) * 3 +
                CallSigned(hidden, value) * 5 + CallHidden(hidden, value) * 7);
        }

        private static int CallThroughBoth(Scorer<int> scorer, int value) =>
            unchecked(CallSigned(scorer, value) + CallInterface(scorer, value) * 11);

        public static int CheckClosedClassNull(int value)
        {
            Scorer<int> scorer = value < 0 ? null : new SignedScorer();
            try { return CallSigned(scorer, value); }
            catch (NullReferenceException) { return -43; }
        }
    }
}
