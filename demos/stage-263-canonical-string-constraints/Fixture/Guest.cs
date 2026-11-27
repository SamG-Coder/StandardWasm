extern alias lookalike;

using System;

public static class Guest
{
    public static int ClosedStringToken()
    {
        Type descriptor = ReadEquatable<string>();
        return descriptor == typeof(Equatable<string, string>) &&
            descriptor != typeof(Equatable<int, int>) ? 101 : 0;
    }

    public static int DifferentCoreInterface() =>
        typeof(Comparable<string>).Name.Length != 0 ? 103 : 0;

    public static int NestedSubstitution()
    {
        Type descriptor = ReadNested<int, string>();
        return descriptor == typeof(Outer<int>.Nested<string>) &&
            descriptor != typeof(Outer<long>.Nested<string>) ? 107 : 0;
    }

    // The oracle changes the first argument of each existing ldtoken to string.
    public static int WrongArgument() => typeof(Equatable<int, int>).Name.Length;
    public static int WrongOwner() => typeof(Lookalike<LocalMatch>).Name.Length;

    private static Type ReadEquatable<T>() where T : IEquatable<T> =>
        typeof(Equatable<T, T>);

    private static Type ReadNested<TOuter, T>() where T : IEquatable<T> =>
        typeof(Outer<TOuter>.Nested<T>);

    private sealed class Equatable<T, TOther> where T : IEquatable<TOther> { }
    private sealed class Comparable<T> where T : IComparable<T> { }

    private sealed class Outer<TOuter>
    {
        public sealed class Nested<T> where T : IEquatable<T> { }
    }

    private sealed class Lookalike<T> where T : lookalike::System.IEquatable<T> { }

    private sealed class LocalMatch : lookalike::System.IEquatable<LocalMatch>
    {
        bool lookalike::System.IEquatable<LocalMatch>.Equals(LocalMatch value) => true;
    }
}
