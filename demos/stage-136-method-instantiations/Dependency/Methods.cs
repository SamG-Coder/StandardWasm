namespace MethodSpecDependency
{
    public static class MethodsFromAnotherAssembly
    {
        public static T[] Pass<T>(T[] values) => values;
        public static int Select<T, U>(T first, U second) => 1;
        public static int Select<T, U>(U first, T second) => 2;
        public static T Forward<T>(T value) => Box<T>.Forward<int>(value, 0);
        public static T Unwrap<T>(Box<T> box) => box.Item;
    }

    public class Box<T>
    {
        public T Item;

        public Box(T item) => Item = item;

        public static T Forward<U>(T value, U other) => Pick<T, U>(value, other);

        private static A Pick<A, B>(A value, B other) => value;
    }
}
