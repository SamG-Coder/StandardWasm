namespace GenericStorageDependency
{
    public interface ILeft { }
    public interface IRight { }

    public sealed class Left : ILeft
    {
        public int Value;
        public Left(int value) => Value = value;
    }

    public sealed class Right : IRight
    {
        public int Value;
        public Right(int value) => Value = value;
    }

    public sealed class Vector<T> where T : class
    {
        public T Entry;
        public T[] Items;
        public int Count;
    }

    public sealed class Frame
    {
        public Vector<ILeft> Left;
        public Vector<IRight> Right;
        public ILeft[] Array;
        public int Marker;

        public Frame(int marker) => Marker = marker;
    }
}
