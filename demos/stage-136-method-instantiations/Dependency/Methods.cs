namespace MethodSpecDependency
{
    public static class MethodsFromAnotherAssembly
    {
        public static T[] Pass<T>(T[] values) => values;
        public static int Select<T, U>(T first, U second) => 1;
        public static int Select<T, U>(U first, T second) => 2;
        public static T Forward<T>(T value) => Box<T>.Forward<int>(value, 0);
        public static T Unwrap<T>(Box<T> box) => box.Item;

        public static T Nominal<T>(T item) where T : IMarker => item;
        public static T Self<T>(T item) where T : ISelf<T> => item;
    }

    public class Box<T>
    {
        public T Item;

        public Box(T item) => Item = item;

        public static T Forward<U>(T value, U other) => Pick<T, U>(value, other);

        public static U Require<U>(U item) where U : IRelation<T, U> => item;

        private static A Pick<A, B>(A value, B other) => value;
    }

    public interface IMarker { }
    public interface ISelf<T> where T : ISelf<T> { }
    public interface IRelation<T, U> { }

    public struct Marker : IMarker, ISelf<Marker>, IRelation<int, Marker>
    {
        public int Value;
        public Marker(int value) => Value = value;
    }
}
