namespace NestedPrivateMembers
{
    public static class Probe
    {
        public static int ReadField() => Container<int>.Cursor.Read(new Container<int>(41));
        public static int WriteField() => Container<int>.Cursor.Write(new Container<int>(-1), 0);
        public static int CallMethod() => Container<long>.Layer.Reader.Call(new Container<long>(int.MaxValue));
    }

    public sealed class Container<T>
    {
        private int version;

        public Container(int version) => this.version = version;

        private int ReadVersion() => version;

        public static class Cursor
        {
            public static int Read(Container<T> container) => container.version;

            public static int Write(Container<T> container, int value)
            {
                container.version = value;
                return container.version;
            }
        }

        public static class Layer
        {
            public static class Reader
            {
                public static int Call(Container<T> container) => container.ReadVersion();
            }
        }
    }
}
