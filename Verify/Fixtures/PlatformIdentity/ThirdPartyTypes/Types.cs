namespace System
{
    public class Object
    {
        public Object() => throw new InvalidOperationException("Third-party base constructor");
    }

    public static class String
    {
        public static string Concat(string left, string right) => right;
    }

    public delegate T Func<T>();

    public sealed class EventArgs
    {
        public static readonly EventArgs Empty = new EventArgs();
    }

    public delegate void EventHandler(object sender, EventArgs args);

    public class Delegate
    {
        public static Delegate Combine(Delegate first, Delegate second) => new Delegate();
    }
}

namespace System.Threading
{
    public static class Interlocked
    {
        public static T CompareExchange<T>(ref T location, T value, T comparand) => comparand;
    }
}
