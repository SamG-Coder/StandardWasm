using System;
using System.Collections.Generic;

namespace ClosedTypeBooleanGuards
{
    public static class Probe
    {
        private static int calls;

        private static bool TrueOperand()
        {
            calls += 2;
            return true;
        }

        private static bool FalseOperand()
        {
            calls += 3;
            return false;
        }

        private sealed class StringOnlyComparer : IEqualityComparer<string>
        {
            public bool Equals(string left, string right) => left == right;
            public int GetHashCode(string value) => value.Length;
        }

        // This generic cast would fail for Int32 if reached. It must be
        // excluded by the proven guard and retained for other instantiations.
        private static int FromStringComparer<T>()
        {
            object candidate = new StringOnlyComparer();
            return ((IEqualityComparer<T>)candidate) == null ? -1 : -2;
        }

        private static int IntAnd<T>(bool enabled)
        {
            if ((!typeof(T).IsValueType) & enabled) return FromStringComparer<T>();
            return 101;
        }

        private static int IntAndCall<T>()
        {
            if ((!typeof(T).IsValueType) & TrueOperand()) return FromStringComparer<T>();
            return 103;
        }

        private static int IntOrCall<T>()
        {
            if (typeof(T).IsValueType | FalseOperand()) return 107;
            return FromStringComparer<T>();
        }

        private static int RuntimeBool<T>(bool enabled)
        {
            if ((!typeof(T).IsValueType) & enabled) return 11;
            return 13;
        }

        private static int JoinedInput<T>(bool choose)
        {
            if ((!typeof(T).IsValueType) & (choose ? TrueOperand() : FalseOperand())) return -1;
            return 113;
        }

        private static int ProtectedInput<T>()
        {
            try
            {
                if ((!typeof(T).IsValueType) & TrueOperand()) return -1;
                return 127;
            }
            finally
            {
                calls += 8;
            }
        }

        public static int PureAndAbsorption() => IntAnd<int>(true);

        public static int EffectfulAndAbsorption()
        {
            calls = 0;
            int result = IntAndCall<int>();
            return calls == 2 ? result : -1;
        }

        public static int EffectfulOrAbsorption()
        {
            calls = 0;
            int result = IntOrCall<int>();
            return calls == 3 ? result : -1;
        }

        public static int ReferenceChoices() =>
            RuntimeBool<string>(true) == 11 && RuntimeBool<string>(false) == 13 ? 109 : -1;

        public static int AlternateInputs()
        {
            calls = 0;
            int first = JoinedInput<int>(true);
            int second = JoinedInput<int>(false);
            return first == 113 && second == 113 && calls == 5 ? 113 : -1;
        }

        public static int FinallyBoundary()
        {
            calls = 0;
            int result = ProtectedInput<int>();
            return calls == 10 ? result : -1;
        }
    }
}
