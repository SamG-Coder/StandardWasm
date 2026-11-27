using System;

public interface IRootReceiver
{
}

public class PlainRootReceiver : IRootReceiver
{
}

public sealed class ComparingRootReceiver : PlainRootReceiver
{
    public override bool Equals(object other) => other is PlainRootReceiver;
    public override int GetHashCode() => 17;
}

public static class RootDispatchProbe
{
    public static int CompareThroughRoot(int mode)
    {
        PlainRootReceiver receiver = null;
        if (mode < 2) receiver = new PlainRootReceiver();
        if (mode == 2) receiver = new ComparingRootReceiver();
        PlainRootReceiver other = mode == 0 ? receiver : new PlainRootReceiver();
        try { return CompareConstrained(receiver, other) ? 1 : 0; }
        catch (NullReferenceException) { return -1; }
    }

    private static bool CompareConstrained<T>(T receiver, T other) where T : IRootReceiver =>
        receiver.Equals(other);
}
