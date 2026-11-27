public static class Probe
{
    public static int VirtualIdentity()
    {
        var value = new Source();
        return Dispatch(new Sink(value), value) ? 101 : 0;
    }

    public static int VirtualNull() => Dispatch<Source>(new Sink(null), null) ? 103 : 0;

    public static int DirectIdentity()
    {
        var value = new Source();
        return Direct(value, value) ? 107 : 0;
    }

    public static int ObjectJoin()
    {
        var value = new Source();
        var other = new object();
        return Choose(true, value, other) == value && Choose(false, value, other) == other ? 109 : 0;
    }

    public static int JoinedInterfaceArgument()
    {
        var value = new Source();
        return SharedConsumer(true, value, new Source(), value) ? 113 : 0;
    }

    private static bool Dispatch<T>(ISink sink, T input) where T : ISource => sink.Compare(input);

    private static bool Direct<T>(ISource expected, T input) where T : ISource => Same(expected, input);

    private static object Choose<T>(bool choose, T input, object other) => choose ? input : other;

    private static bool SharedConsumer<T>(bool choose, T input, ISource other, ISource expected) where T : ISource =>
        Same(expected, choose ? input : other);

    private static bool Same(ISource expected, ISource actual) => expected == actual;

    private interface ISource { }
    private sealed class Source : ISource { }

    private interface ISink
    {
        bool Compare(ISource actual);
    }

    private sealed class Sink : ISink
    {
        private readonly ISource expected;

        internal Sink(ISource expected) { this.expected = expected; }

        public bool Compare(ISource actual) => expected == actual;
    }
}
