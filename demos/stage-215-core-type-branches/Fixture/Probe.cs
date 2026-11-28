using System.Runtime.CompilerServices;

namespace CoreTypeBranches
{
    public static class Probe
    {
        private sealed class Key { }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int Equal<T>()
        {
            if (typeof(T) == typeof(string)) return 923;
            return 929;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int Unequal<T>()
        {
            if (typeof(T) != typeof(string)) return 937;
            return 941;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int PeerTypes<T, U>()
        {
            if (typeof(T) == typeof(U)) return 947;
            return 953;
        }

        public static int StringEquality() => Equal<string>();
        public static int ReferenceEquality() => Equal<Key>();
        public static int StringInequality() => Unequal<string>();
        public static int ReferenceInequality() => Unequal<Key>();
        public static int SameClosedArguments() => PeerTypes<Key, Key>();
        public static int DifferentClosedArguments() => PeerTypes<Key, string>();
        public static int ValueKeyEquality() => Equal<int>();
    }
}
