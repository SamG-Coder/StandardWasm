using System;
using System.Reflection;

namespace RuntimeGenericArgumentsDemo
{
    public interface IValue<T> { }
    public sealed class Outer<T>
    {
        public sealed class Inner<U> { }
    }

    public static class Probe
    {
        public static int ClosedValueType() =>
            typeof(int?).GetGenericArguments()[0] == typeof(int) ? 101 : 0;

        public static int NestedArgumentOrder()
        {
            Type[] args = typeof(Outer<string>.Inner<long>).GetGenericArguments();
            return args.Length == 2 && args[0] == typeof(string) && args[1] == typeof(long) ? 103 : 0;
        }

        public static int FreshMutableArray()
        {
            Type t = typeof(IValue<int>);
            Type[] first = t.GetGenericArguments();
            Type[] second = t.GetGenericArguments();
            first[0] = typeof(uint);
            return !ReferenceEquals(first, second) && first[0] == typeof(uint) && second[0] == typeof(int) ? 107 : 0;
        }

        public static int SharedEmptyArray()
        {
            Type[] first = typeof(int).GetGenericArguments();
            Type[] second = typeof(string).GetGenericArguments();
            return first.Length == 0 && ReferenceEquals(first, second) ? 109 : 0;
        }

        public static int ArrayOfClosedGeneric() =>
            typeof(IValue<string>[][]).GetGenericArguments().Length;

        public static int RuntimeArrayIdentity() =>
            typeof(IValue<int>).GetGenericArguments().GetType() == typeof(Type[]) ? 113 : 0;

        public static int NullReceiver()
        {
            try { return ReadArgumentLength(false); }
            catch (NullReferenceException) { return 127; }
        }

        private static int ReadArgumentLength(bool initialized)
        {
            Type type = null;
            if (initialized) type = typeof(IValue<int>);
            return type.GetGenericArguments().Length;
        }

        public static int NominalOverride()
        {
            Type wrapped = new CustomType();
            return wrapped.GetGenericArguments().Length == 3 ? 131 : 0;
        }

        public static int Bounds()
        {
            try { return typeof(IValue<int>).GetGenericArguments()[1] == null ? 1 : 0; }
            catch (IndexOutOfRangeException) { return 137; }
        }

        private sealed class CustomType : TypeDelegator
        {
            public override Type[] GetGenericArguments() => new Type[3];
        }
    }
}
