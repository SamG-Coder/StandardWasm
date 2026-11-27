using System;

namespace InterfaceDelegateAbi
{
    public interface IValue
    {
        int Read();
    }

    public sealed class Value : IValue
    {
        private readonly int number;
        public Value(int number) { this.number = number; }
        public int Read() => number;
    }

    public static class Probe
    {
        private delegate int ReadValue(IValue value);
        private interface IAlternateValue { }
        private static class Slot<T> { internal static int Value; }

        private sealed class Capture
        {
            private readonly int offset;
            internal Capture(int offset) { this.offset = offset; }
            internal int Read(IValue value) => value.Read() + offset;
            internal IValue Make(int value) => new Value(value + offset);
        }

        private static int ReadOrFallback(IValue value) => value == null ? -79 : value.Read();

        public static int CapturedInterfaceArgument()
        {
            Func<IValue, int> read = new Capture(17).Read;
            return read(new Value(25));
        }

        public static int CapturedInterfaceReturn()
        {
            Func<int, IValue> make = new Capture(37).Make;
            return make(61).Read();
        }

        public static int NullInterfaceArgument()
        {
            Func<IValue, int> read = ReadOrFallback;
            return read(null);
        }

        public static int DistinctNominalInterfaces()
        {
            Slot<Func<IValue, int>>.Value = 19;
            Slot<Func<IAlternateValue, int>>.Value = 31;
            return Slot<Func<IValue, int>>.Value + 2 * Slot<Func<IAlternateValue, int>>.Value;
        }

        public static int NominalDelegateDefinition()
        {
            ReadValue read = new Capture(23).Read;
            return read(new Value(84));
        }
    }
}
