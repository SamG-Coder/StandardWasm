namespace GenericBodyDefinition
{
    // One compiled generic definition. Each closed instantiation needs its own
    // constructor and method bodies because T fixes field, parameter, return
    // and local storage, for example i8 versus u8 packed fields.
    public class Holder<T>
    {
        private T value;
        private int writes;

        public Holder(T initial)
        {
            value = initial;
            writes = 1;
        }

        public T Get() => value;

        public int Writes => writes;

        public void Set(T next) => Store(next);

        // Private member of the same definition, reached through Holder<!0>.
        private void Store(T next)
        {
            value = next;
            writes++;
        }

        public T Swap(T next)
        {
            T previous = value; // A local whose type is the class parameter.
            Store(next);
            return previous;
        }

        public Holder<T> Copy() => new Holder<T>(value);

        public static Holder<T> Create(T initial) => new Holder<T>(initial);

        public static T Pick(bool first, Holder<T> left, Holder<T> right) => first ? left.value : right.value;

        // Outside the direct-specialization scope; the compiler must reject these.
        public U Echo<U>(U item) => item;

        public virtual int Describe() => writes;
    }
}
