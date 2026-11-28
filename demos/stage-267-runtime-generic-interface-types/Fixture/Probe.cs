using System;

namespace RuntimeGenericInterfaces
{
    public interface IChosen<T> { }

    public interface IAlternate<T> { }

    public enum Choice { First }

    public sealed class Marker { }

    public sealed class LiteralOnly { }

    public static class Probe
    {
        public static int StringIdentity()
        {
            Type result = typeof(IChosen<>).MakeGenericType(typeof(string));
            return result == typeof(IChosen<string>) ? 101 : 0;
        }

        public static int RuntimeChoice()
        {
            return RuntimeChoice(false) + RuntimeChoice(true);
        }

        private static int RuntimeChoice(bool unsigned)
        {
            Type argument = typeof(int);
            if (unsigned) argument = typeof(uint);
            Type result = typeof(IChosen<>).MakeGenericType(argument);
            if (result == typeof(IChosen<int>)) return 49;
            if (result == typeof(IChosen<uint>)) return 54;
            return 0;
        }

        public static int SeparateDefinitions()
        {
            Type first = typeof(IChosen<>).MakeGenericType(typeof(object));
            Type second = typeof(IAlternate<>).MakeGenericType(typeof(object));
            return first != second && second == typeof(IAlternate<object>) ? 107 : 0;
        }

        public static int NullArgument()
        {
            try
            {
                return NullArgument(false);
            }
            catch (ArgumentNullException)
            {
                return 109;
            }
        }

        private static int NullArgument(bool useValue)
        {
            Type argument = null;
            if (useValue) argument = typeof(int);
            Type result = typeof(IChosen<>).MakeGenericType(argument);
            return result == typeof(IChosen<int>) ? 1 : 0;
        }

        public static int RepeatWithoutClosedToken()
        {
            Type first = typeof(IAlternate<>).MakeGenericType(typeof(long));
            Type second = typeof(IAlternate<>).MakeGenericType(typeof(long));
            return ReferenceEquals(first, second) ? 127 : 0;
        }

        public static int EnumTypeArgument()
        {
            Type result = typeof(IChosen<>).MakeGenericType(typeof(Choice));
            return result == typeof(IChosen<Choice>) && result != typeof(IChosen<int>) ? 131 : 0;
        }

        public static int ObjectDiscoveredArgument()
        {
            Type argument = new Marker().GetType();
            Type result = typeof(IAlternate<>).MakeGenericType(argument);
            return result == typeof(IAlternate<Marker>) ? 137 : 0;
        }

        public static int NominalPrimitiveLayout()
        {
            int value = 42;
            if (!Equal(ref value, 42)) return 0;
            Type actual = typeof(IChosen<>).MakeGenericType(typeof(int));
            return actual == typeof(IChosen<int>) ? 139 : 0;
        }

        private static bool Equal<T>(ref T value, object other) => value.Equals(other);

        public static int BareNominalType()
        {
            Type actual = typeof(IChosen<>).MakeGenericType(typeof(LiteralOnly));
            return actual != null ? 149 : 0;
        }

        public static int VoidTypeArgument()
        {
            try
            {
                typeof(IChosen<>).MakeGenericType(typeof(void));
                return 0;
            }
            catch (ArgumentException)
            {
                return 151;
            }
        }

        public static int NestedLiteralFactory()
        {
            Type first = typeof(IChosen<>).MakeGenericType(typeof(int));
            Type second = typeof(IAlternate<>).MakeGenericType(first);
            return second != null ? 157 : 0;
        }

        public static int ArrayTypeArgument()
        {
            Type result = typeof(IChosen<>).MakeGenericType(typeof(int[]));
            return result == typeof(IChosen<int[]>) ? 113 : 0;
        }
    }
}
