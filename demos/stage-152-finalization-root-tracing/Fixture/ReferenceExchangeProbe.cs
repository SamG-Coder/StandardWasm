using System;
using System.Threading;

namespace ManagedFinalizationExperiment
{
    public static class ReferenceExchangeProbe
    {
        private static Token published;

        public static int Check(int scenario)
        {
            if (scenario == 5) return CheckObjectOverload();
            if (scenario == 6) return CheckString();
            if (scenario == 7 || scenario == 8) return CheckInstanceField(scenario == 8);
            if (scenario == 9) return CheckNullReceiver();
            if (scenario == 10) return CheckEventField();
            var first = new Token(31);
            var replacement = scenario == 3 ? null : new Token(47);
            published = scenario == 0 ? null : first;
            Token comparand = scenario == 0 || scenario == 4 ? null :
                scenario == 2 ? new Token(31) : first;
            Token previous = Interlocked.CompareExchange(ref published, replacement, comparand);
            return (previous == null ? 0 : previous.Value) * 100 + (published == null ? 0 : published.Value);
        }

        private static int CheckObjectOverload()
        {
            var initial = new object();
            var replacement = new object();
            object slot = initial;
            object previous = Interlocked.CompareExchange(ref slot, replacement, initial);
            return ReferenceEquals(previous, initial) && ReferenceEquals(slot, replacement) ? 107 : -1;
        }

        private static int CheckString()
        {
            string slot = "abc";
            string previous = Interlocked.CompareExchange(ref slot, "xy", "abc");
            return previous.Length * 100 + slot.Length;
        }

        private static int CheckInstanceField(bool exchangeMisses)
        {
            var first = new DerivedSlot();
            var other = new DerivedSlot();
            if (first.Value != null || other.Value != null) return -1;
            ref Token location = ref first.Value;
            var initial = new Token(11);
            first.Store(initial);
            other.Store(new Token(37));
            var comparand = exchangeMisses ? new Token(11) : initial;
            Token previous = Interlocked.CompareExchange(ref location, new Token(23), comparand);
            int result = previous.Value * 10000 + first.Value.Value * 100 + other.Value.Value;
            location = null;
            return first.Value == null ? result : -2;
        }

        private static int CheckNullReceiver()
        {
            Slot missing = null;
            try { return Interlocked.CompareExchange(ref missing.Value, null, null) == null ? -1 : -2; }
            catch (NullReferenceException) { return 73; }
        }

        private static int CheckEventField()
        {
            var slot = new Slot();
            var handler = new EventHandler(slot.Record);
            slot.Handler = handler;
            ref EventHandler location = ref slot.Handler;
            EventHandler previous = Interlocked.CompareExchange(ref location, null, handler);
            previous(slot, null);
            return slot.Handler == null ? slot.Observed : -3;
        }

        private class Slot
        {
            internal Token Value;
            internal EventHandler Handler;
            internal int Observed;

            internal void Store(Token value) => Value = value;
            internal void Record(object sender, EventArgs args) => Observed = 19;
        }

        private sealed class DerivedSlot : Slot { }

        private sealed class Token
        {
            internal readonly int Value;

            internal Token(int value) => Value = value;
        }
    }
}
