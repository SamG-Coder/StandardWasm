using System.Collections.Generic;

public static class ClosedComparerCastProbe
{
    private class TextComparer : IEqualityComparer<string>
    {
        public bool Equals(string x, string y) => x == y;
        public int GetHashCode(string value) => value.Length;
    }

    private sealed class DerivedTextComparer : TextComparer { }

    private sealed class NumberComparer : IEqualityComparer<int>
    {
        public bool Equals(int x, int y) => x == y;
        public int GetHashCode(int value) => value;
    }

    private static IEqualityComparer<string> CastText(TextComparer value) =>
        (IEqualityComparer<string>)(object)value;

    private static IEqualityComparer<string> CastDerived(DerivedTextComparer value) =>
        (IEqualityComparer<string>)(object)value;

    private static IEqualityComparer<string> CastWrong(NumberComparer value) =>
        (IEqualityComparer<string>)(object)value;

    private static IEqualityComparer<string> CastWidened(object value) =>
        (IEqualityComparer<string>)value;

    public static int ExactClass()
    {
        var source = new TextComparer();
        return ReferenceEquals(source, CastText(source)) ? 101 : -1;
    }

    public static int InheritedClass()
    {
        var source = new DerivedTextComparer();
        return ReferenceEquals(source, CastDerived(source)) ? 103 : -1;
    }

    public static int Null() => CastText(null) == null ? 107 : -1;
    public static int WrongArgument() => CastWrong(new NumberComparer()) == null ? 0 : 1;

    public static int Widened()
    {
        var source = new TextComparer();
        return ReferenceEquals(source, CastWidened(source)) ? 109 : -1;
    }
}
