using System;

namespace StaticStorageDependency
{
    public sealed class Token
    {
        public int Value;
    }

    public static class Storage
    {
        public static int Counter;
        public static short Narrow;
        public static long Wide;
        public static float Single;
        public static double Double;
        public static object Key;
        public static Token Token;

        [ThreadStatic]
        public static int ThreadCounter;

        public static int Add(int delta)
        {
            Counter = unchecked(Counter + delta);
            return Counter;
        }
    }

    public static class Initialized
    {
        public static int Counter = 42;
    }
}
