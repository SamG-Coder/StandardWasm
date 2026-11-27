using System;

public interface IConstrainedReceiver
{
    int ReadConstrainedValue(int value);
}

public class ConstrainedReceiver : IConstrainedReceiver
{
    public virtual int ReadConstrainedValue(int value) => value + 2;
}

public sealed class DerivedConstrainedReceiver : ConstrainedReceiver
{
    public override int ReadConstrainedValue(int value) => value + 7;
}

public static class ConstrainedReferenceProbe
{
    public static int InvokeConstrainedReceiver(int mode)
    {
        ConstrainedReceiver receiver = null;
        if (mode == 0) receiver = new ConstrainedReceiver();
        if (mode == 1) receiver = new DerivedConstrainedReceiver();
        try { return ReadConstrained(receiver, 4); }
        catch (NullReferenceException) { return -1; }
    }

    public static int CheckWidenedIdentity(int mode)
    {
        IConstrainedReceiver first = mode == 0 ? null : new ConstrainedReceiver();
        IConstrainedReceiver second = mode == 2 ? new ConstrainedReceiver() : first;
        return SameReference(first, second) ? 1 : 0;
    }

    private static int ReadConstrained<T>(T receiver, int value) where T : IConstrainedReceiver =>
        receiver.ReadConstrainedValue(value);

    private static bool SameReference<T>(T first, T second) => (object)first == (object)second;
}
