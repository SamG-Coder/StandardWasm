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

public static class Execution
{
    public static int Integer(int value) => Pick(value, -17, 2);
    public static int Wide(int value) => unchecked((int)(Pick((long)value << 32, 93L, 0) >> 32));
    public static int Text(int value) => Choose(Reference("left"), "wrong", value).Length;
    public static int Recurse(int value) => Repeat(value, 4096);
    public static int Value(int value) => Structure(Choose(new Cell(value), new Cell(3), 0)).Value;

    public static int Array(int value) => EchoArray(new[] { value })[0];

    private static T Reference<T>(T value) where T : class => value;
    private static T Structure<T>(T value) where T : struct => value;
    private static T[] EchoArray<T>(T[] values) => values;

    private static T Pick<T>(T first, T other, int updates)
    {
        T selected = first;
        for (int i = 0; i < updates; i++)
            selected = other;
        return selected;
    }

    private static T Choose<T>(T first, T other, int choice) => choice == 0 ? first : other;

    private static T Repeat<T>(T value, int remaining) => remaining == 0 ? value : Repeat(value, remaining - 1);
}

public struct Cell
{
    public int Value;
    public Cell(int value) => Value = value;
}
