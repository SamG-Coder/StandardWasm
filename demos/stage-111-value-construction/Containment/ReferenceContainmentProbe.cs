using System.Runtime.CompilerServices;

namespace ValueConstructionDemo
{
    public static class ReferenceContainmentProbe
    {
        private enum Marker : uint { None }

        private struct StaticReferenceOnly
        {
            public static readonly object Shared = new object();
            public readonly int Number;

            public StaticReferenceOnly(int number) => Number = number;
        }

        private struct Wrapper<T>
        {
            public readonly T Value;

            public Wrapper(T value) => Value = value;
        }

        // Returning the intrinsic directly and branching on it exercise both lowering paths.
        private static bool IsTraced<T>() => RuntimeHelpers.IsReferenceOrContainsReferences<T>();

        private static int Branch<T>(int input)
        {
            if (RuntimeHelpers.IsReferenceOrContainsReferences<T>()) return input ^ 17;
            return input ^ 31;
        }

        public static int CheckReferenceContainment(int input)
        {
            int mask = IsTraced<int>() ? 1 : 0;
            if (IsTraced<Marker>()) mask |= 2;
            if (IsTraced<StaticReferenceOnly>()) mask |= 4;
            if (IsTraced<int[]>()) mask |= 8;
            if (IsTraced<string>()) mask |= 16;
            if (IsTraced<Wrapper<Wrapper<string>>>()) mask |= 32;
            if (IsTraced<Wrapper<Wrapper<int>>>()) mask |= 64;
            return unchecked(input * mask) ^ Branch<Wrapper<string>>(input) ^ Branch<int>(input + 1);
        }
    }
}
