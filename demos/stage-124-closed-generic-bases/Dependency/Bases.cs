namespace GenericBaseDependency
{
    public class Root
    {
        public virtual int Code() => 3;
    }

    public class Box<T> : Root
    {
        public T Value;
        public Box(T value) { Value = value; }
    }

    public class Nested<T> : Box<T[]>
    {
        public Nested(T[] value) : base(value) { }
    }

    public class Forward<T> : Box<T>
    {
        public Forward(T value) : base(value) { }
    }
}
