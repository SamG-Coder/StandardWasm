using System;

namespace CoreTypeComparisonFixture
{
    public static class Probe
    {
        private static bool IsInteger(Type type) => type == typeof(int);
        private static bool IsNotInteger(Type type) => type != typeof(int);

        public static int MatchingRuntimeType() => IsInteger(typeof(int)) ? 1 : 0;
        public static int DifferentRuntimeType() => IsInteger(typeof(uint)) ? 1 : 0;
        public static int NullRuntimeType() => IsInteger(Type.GetTypeFromHandle(default(RuntimeTypeHandle))) ? 1 : 0;
        public static int MatchingInequality() => IsNotInteger(typeof(int)) ? 1 : 0;
        public static int DifferentInequality() => IsNotInteger(typeof(uint)) ? 1 : 0;
        public static int NullInequality() => IsNotInteger(Type.GetTypeFromHandle(default(RuntimeTypeHandle))) ? 1 : 0;
    }
}
