using System;

namespace DelegateVarianceFixture
{
    public static class Probe
    {
        private interface IValue { int Read(); }
        private sealed class Value : IValue
        {
            private readonly int value;
            internal Value(int value) { this.value = value; }
            public int Read() => value;
        }

        private sealed class Capture
        {
            private readonly Value result;
            internal Capture(int value) { result = new Value(value); }
            internal Value FromInterface(IValue value) => value == null ? null : result;
            internal Value FromObject(object value) => value == null ? null : result;
        }

        private static int observed;
        private static void ReadObject(object value) { observed = ((Value)value).Read(); }
        private static object Invoke(Func<IValue, object> callback, IValue input) => callback(input);
        private static Func<IValue, object> Widen(Func<IValue, Value> callback) => callback;

        public static int CovariantReturn()
        {
            Func<IValue, Value> original = new Capture(41).FromInterface;
            Func<IValue, object> view = Widen(original);
            return ReferenceEquals(original, view) ? ((Value)Invoke(view, new Value(1))).Read() : -1;
        }

        public static int ContravariantArgument()
        {
            Action<object> original = ReadObject;
            Action<Value> view = original;
            view(new Value(83));
            return ReferenceEquals(original, view) ? observed : -1;
        }

        public static int BothDirections()
        {
            Func<object, Value> original = new Capture(127).FromObject;
            Func<Value, object> view = original;
            return ReferenceEquals(original, view) ? ((Value)view(new Value(1))).Read() : -1;
        }

        public static int PreservesNull()
        {
            Func<IValue, Value> original = new Capture(15).FromInterface;
            return Widen(null) == null && Invoke(original, null) == null ? 1 : 0;
        }
    }
}
