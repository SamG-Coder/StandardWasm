namespace ConstrainedInt32ToString;

public static class Probe
{
    // constrained. !!T callvirt Object::ToString. With T = int the CLR calls
    // the Int32.ToString override on the managed receiver address without boxing.
    private static string Format<T>(T value) => value.ToString();

    // box int32, then callvirt Object::ToString through the boxed copy.
    private static string FormatBoxed(int value)
    {
        object boxed = value;
        return boxed.ToString();
    }

    // Constrained Int32 Equals keeps its earlier direct-scalar binding.
    private static bool Same<T>(T left, object right) => left.Equals(right);

    // Constrained Int32 GetHashCode keeps its existing exact-value binding.
    private static int Hash<T>(T value) => value.GetHashCode();

    // A value type that inherits Object.ToString: the CLR boxes the receiver.
    // This shape stays an explicit diagnostic.
    private static string Inherited<T>(T value) => value.ToString();

    public static int Zero() => Format(0) == "0" ? 1 : 0;

    public static int Positive() => Format(4125) == "4125" ? 1 : 0;

    public static int Minimum() => Format(int.MinValue) == "-2147483648" ? 1 : 0;

    public static int Maximum() => Format(int.MaxValue) == "2147483647" ? 1 : 0;

    public static int BoxedPositive() => FormatBoxed(4125) == "4125" ? 1 : 0;

    public static int BoxedMinimum() => FormatBoxed(int.MinValue) == "-2147483648" ? 1 : 0;

    // The constrained receiver reads the current value of the caller's local.
    public static int UpdatedLocal()
    {
        int value = 7;
        value += 35;
        return Format(value) == "42" && value == 42 ? 1 : 0;
    }

    public static int EqualsStillDirect() => Same(5, 5) && !Same(5, 6) ? 1 : 0;

    public static int AuthoredValue()
    {
        var value = new Counter(3);
        return Format(value) == "advanced" && value.Value == 3 ? 1 : 0;
    }

    public static int Int32Hash() => Hash(9) == 9 ? 1 : 0;

    public static int InheritedValue() => Inherited(new Plain()) == "ConstrainedInt32ToString.Probe+Plain" ? 1 : 0;

    public struct Counter
    {
        public int Value;

        public Counter(int value) => Value = value;

        // The override mutates Format's parameter copy, so the caller keeps 3.
        public override string ToString()
        {
            Value++;
            return Value == 4 ? "advanced" : "unexpected";
        }
    }

    public struct Plain
    {
        public int Value;
    }
}
