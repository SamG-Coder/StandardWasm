namespace DefaultConstructors
{
    public interface ISource { }
    public sealed class Source : ISource
    {
        public int Value;

        public Source(int value) { Value = value; }
        public Source() { }
    }

    public sealed class GenericSource<T> { }
    public struct ValueSource { public int Value; }

    public static class Probe
    {
        private static int Accept<T>(int marker) where T : new() => marker;
        private static int AcceptSource<T>(int marker) where T : ISource, new() => marker;

        public static int OverloadedReference() => Accept<Source>(151);
        public static int GenericReference() => Accept<GenericSource<Source>>(157);
        public static int InterfaceAndConstructor() => AcceptSource<Source>(163);
        public static int RootObject() => Accept<object>(167);
        public static int OrdinaryStruct() => Accept<ValueSource>(173);
        public static int Primitive() => Accept<int>(179);
    }
}
