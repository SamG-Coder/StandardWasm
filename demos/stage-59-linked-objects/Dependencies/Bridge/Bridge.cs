using ObjectLeaf;

namespace ObjectBridge
{
    public sealed class State
    {
        public long Count;
        public Tail End;
        public int Adjustment;

        public State(int value)
        {
            Count = (long)value * 2;
            Adjustment = 2;
        }
    }

    public sealed class Tail
    {
        public State Owner;
        public int Delta;

        public int Score() => unchecked((int)Owner.Count + Owner.Adjustment + Delta);
        public int Constant() => 17;
    }

    public static class Bridge
    {
        public static int Compute(int value)
        {
            var state = new State(value);
            state.End = new Tail { Owner = state, Delta = 5 };
            return unchecked(state.End.Score() + Leaf.Compute(value));
        }

        public static int Touch(int present)
        {
            var state = new State(0);
            if (present != 0) state.End = new Tail();
            return state.End.Constant();
        }

        public static object Make(int value) => Leaf.Make(value);
        public static object Echo(object value) => Leaf.Echo(value);
    }
}
