using System.Numerics;
using System.Reflection;
using System.Text.Json;

public static class CoreDispatch
{
    public static int Sign(int value) => IsNegative<long>(value) ? 1 : 0;

    public static int Score(int value) => value < 0
        ? Measure(new First(value))
        : Measure(new Second(value));

    private static bool IsNegative<T>(T value) where T : INumberBase<T> => T.IsNegative(value);
    private static int Measure<T>(T value) where T : IMeasure<T> => T.Score(value);

    public static void Main(string[] args)
    {
        var methods = new[] { typeof(CoreDispatch).GetMethod(nameof(Sign))!, typeof(CoreDispatch).GetMethod(nameof(Score))! };
        var entries = methods.Select(method => new { name = method.Name, row = method.MetadataToken & 0xffffff });
        var calls = methods.SelectMany(method => new[] { int.MinValue, -1, 0, 1, int.MaxValue }.Select(input =>
            new { name = method.Name, input, expected = (int)method.Invoke(null, new object[] { input })! }));
        File.WriteAllText(args[0], JsonSerializer.Serialize(new { entries, calls }));
    }
}

public interface IMeasure<T> where T : IMeasure<T>
{
    static abstract int Score(T value);
}

public interface IAlternate<T> where T : IAlternate<T>
{
    static abstract int Score(T value);
}

public readonly struct First : IMeasure<First>, IAlternate<First>
{
    private readonly int value;
    public First(int value) => this.value = value;
    static int IMeasure<First>.Score(First value) => value.value ^ 7;
    static int IAlternate<First>.Score(First value) => value.value ^ 23;
}

public readonly struct Second : IMeasure<Second>
{
    private readonly int value;
    public Second(int value) => this.value = value;
    public static int Score(Second value) => value.value ^ 13;
}
