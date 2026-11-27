namespace InterfaceConstraints
{
    public interface IFlag { }

    public interface IItem<T> { }

    public interface INumberItem : IItem<int> { }

    public sealed class NumberItem : INumberItem { }

    public static class Probe
    {
        private static int ReadFlag<T>(int value) where T : IFlag => value + 1;

        private static int ReadNumber<T>(int value) where T : IItem<int> => value + 2;

        private sealed class Outer<TResult>
        {
            public sealed class Box<TItem> where TItem : IItem<TResult>
            {
                public int Value;

                public Box(int value) { Value = value; }
            }
        }

        public static int PlainInterfaceMethod() => ReadFlag<IFlag>(100);

        public static int ClosedInterfaceMethod() => ReadNumber<IItem<int>>(101);

        public static int DerivedInterfaceMethod() => ReadNumber<INumberItem>(105);

        public static int ConcreteImplementation() => ReadNumber<NumberItem>(107);

        public static int NestedInterfaceLocal()
        {
            var text = new Outer<string>.Box<IItem<string>>(14);
            var number = new Outer<int>.Box<IItem<int>>(99);
            return text.Value + number.Value;
        }
    }
}
