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

        private sealed class Token
        {
            internal readonly int Value;

            internal Token(int value) => Value = value;
        }
    }
}
