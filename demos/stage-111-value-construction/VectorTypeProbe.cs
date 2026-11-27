using System;

namespace ValueConstructionDemo
{
    public static class VectorTypeProbe
    {
        private class Element { }
        private sealed class Derived : Element { }
        private enum Number { Zero }

        private static Type VectorOf<T>() => typeof(T[]);
        private static int Identify(object value) => value.GetType() == typeof(Derived[]) ? 128 : 0;

        public static int CheckVectorTypes(int mode)
        {
            int result = VectorOf<byte>() == typeof(byte[]) ? 1 : 0;
            if (typeof(byte[]) != typeof(bool[])) result |= 2;
            if (typeof(byte[]) != typeof(sbyte[])) result |= 4;
            if (VectorOf<int[]>() == typeof(int[][])) result |= 8;
            if (typeof(int[][]) != typeof(uint[][])) result |= 16;
            if (VectorOf<Number>() == typeof(Number[]) && typeof(Number[]) != typeof(int[])) result |= 32;
            if (typeof(Derived[]) != typeof(Element[])) result |= 64;
            if (mode < 0) result |= Identify(new Derived[1]);
            else result |= Identify(new Element[1]);
            return result;
        }
    }
}
