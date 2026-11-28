using System.Collections.Generic;

namespace ReadOnlyArrayAssignment;

public struct Value
{
    public int Number;
}

public sealed class Reference
{
    public int Number;
}

public static class Probe
{
    private static int ObserveList(IReadOnlyList<Value> view, object array) =>
        view == null ? 109 : ReferenceEquals(view, array) ? 101 : -1;

    private static int ObserveCollection(IReadOnlyCollection<Reference> view, object array) =>
        view == null ? 107 : ReferenceEquals(view, array) ? 103 : -1;

    private static int ObserveCovariant(IReadOnlyList<object> view, object array) =>
        ReferenceEquals(view, array) ? 113 : -1;

    public static int ValueList()
    {
        var values = new Value[2];
        return ObserveList(values, values);
    }

    public static int ReferenceCollection()
    {
        var values = new Reference[1];
        return ObserveCollection(values, values);
    }

    public static int NullList() => ObserveList(null, null);

    public static int NullCollection() => ObserveCollection(null, null);

    public static int RequiresVariance()
    {
        var values = new Reference[1];
        return ObserveCovariant(values, values);
    }

    public static int RequiresDispatch()
    {
        IReadOnlyList<Value> values = new Value[3];
        return values.Count;
    }
}
