using System;
using System.Linq;
using MethodSpecDependency;

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

    public static int LinkedArray(int value) => MethodsFromAnotherAssembly.Pass(new[] { value })[0];
    public static int LinkedOverloads(int value) =>
        MethodsFromAnotherAssembly.Select<int, uint>(value, (uint)value) * 10 +
        MethodsFromAnotherAssembly.Select<int, uint>((uint)value, value);
    public static int LinkedDeclaringType(int value) => Box<int>.Forward<long>(value, 3L);
    public static int LinkedParentVariable(int value) => MethodsFromAnotherAssembly.Forward(value);
    public static int LinkedConstructed(int value) => MethodsFromAnotherAssembly.Unwrap(new Box<int>(value));
    public static int NominalConstraint(int value) => MethodsFromAnotherAssembly.Nominal(new Marker(value)).Value;
    public static int SelfConstraint(int value) => MethodsFromAnotherAssembly.Self(new Marker(value)).Value;
    public static int MixedConstraint(int value) => Box<int>.Require(new Marker(value)).Value;

    public static int BoxInteger(int value) => Unpack<int>(Pack(value));
    public static int BoxWide(int value) => unchecked((int)(Unpack<long>(Pack((long)value << 32)) >> 32));
    public static int BoxSingle(int value) => 1f / Unpack<float>(Pack(value < 0 ? -0f : 0f)) < 0f ? 1 : 0;
    public static int BoxDouble(int value) => 1d / Unpack<double>(Pack(value < 0 ? -0d : 0d)) < 0d ? 1 : 0;
    public static int NominalBox(int value) => IsInt(Pack(value)) && !IsInt(Pack((uint)value)) && !IsInt(new object()) ? 1 : 0;
    public static int FreshBox(int value) => Pack(value) == Pack(value) ? 1 : 0;

    public static int WrongBox(int value)
    {
        try { return Unpack<int>(Pack((uint)value)); }
        catch (InvalidCastException) { return -17; }
    }

    public static int NullBox(int value)
    {
        try { return Unpack<int>(null); }
        catch (NullReferenceException) { return value; }
    }

    private static object Pack<T>(T value) => value;
    private static T Unpack<T>(object value) => (T)value;
    private static bool IsInt(object value) => value is int;

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
