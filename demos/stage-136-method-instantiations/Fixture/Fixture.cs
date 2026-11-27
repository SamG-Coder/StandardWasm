using System;
using System.Linq;

public static class Methods
{
    public static int Local() => Identity(3);
    public static long External() => Enumerable.Repeat(6L, 1).First();
    private static T Identity<T>(T value) => value;
}
public class Context<T>
{
    public static U Mixed<U>(T value, U other) => Select<T, U>(value, other);
    private static B Select<A, B>(A value, B other) => other;
    public static T[] Empty() => Array.Empty<T>();
}
