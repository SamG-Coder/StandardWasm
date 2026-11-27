using System;

public sealed class RootAnchor
{
    public sealed class Child { }
}

public sealed class RootGeneric<T> { }

namespace Café.Δ
{
    public sealed class Anchor
    {
        public sealed class Child
        {
            public sealed class Leaf { }
        }
    }

    public sealed class GenericAnchor<T>
    {
        public sealed class Child<U> { }
    }

    public enum Locale : byte { Default }
}

namespace TypeNamespaceFixture
{
    public static class Probe
    {
        private static int Code(Type type)
        {
            string value = type.Namespace;
            if (value == null) return 2;
            if (value == "Café.Δ") return 3;
            if (value == "System") return 5;
            if (value == "Companion.É") return 7;
            return -100;
        }

        public static int Definitions() => Code(typeof(Café.Δ.Anchor)) + 10 * Code(typeof(RootAnchor));

        public static int NestedDefinitions() =>
            Code(typeof(Café.Δ.Anchor.Child.Leaf)) + 10 * Code(typeof(RootAnchor.Child));

        public static int GenericDefinitions() =>
            Code(typeof(Café.Δ.GenericAnchor<>)) + 10 * Code(typeof(RootGeneric<>));

        public static int GenericConstructed() =>
            Code(typeof(Café.Δ.GenericAnchor<RootAnchor>)) +
            10 * Code(typeof(RootGeneric<Café.Δ.Anchor>));

        public static int NestedGenericConstructed() =>
            Code(typeof(Café.Δ.GenericAnchor<RootAnchor>.Child<Companion.É.Anchor>));

        public static int PlatformDescriptors() =>
            CodeOf<int>() + 10 * Code(typeof(string)) + 100 * Code(typeof(object));

        private static int CodeOf<T>() => Code(typeof(T));

        public static int PlatformVectors() =>
            Code(typeof(byte[])) + 10 * Code(typeof(int[][])) + 100 * Code(typeof(string[]));

        public static int NominalVectors() =>
            Code(typeof(Café.Δ.Locale[])) + 10 * Code(typeof(RootAnchor[])) + 100 * Code(typeof(Café.Δ.Anchor.Child[][]));

        public static int ClosedVector() => Code(typeof(Café.Δ.GenericAnchor<RootAnchor>[]));

        public static int LinkedNested() => Code(typeof(Companion.É.Anchor.Child));

        public static int RuntimeSelection() => 10 * Code(Select(true)) + Code(Select(false));

        private static Type Select(bool first)
        {
            if (first) return new Café.Δ.Anchor().GetType();
            return new RootAnchor().GetType();
        }

        public static int NameAndNamespace()
        {
            Type source = typeof(Café.Δ.Anchor.Child);
            string name = source.Name;
            return name == "Child" ? 10 * Code(source) + 1 : -1;
        }

        public static int NullReceiver()
        {
            Type type = null;
            try { return type.Namespace == null ? 0 : 1; }
            catch (NullReferenceException) { return 13; }
        }
    }
}
