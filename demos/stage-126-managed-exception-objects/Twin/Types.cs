namespace Shared
{
    // The fixture assembly declares a class with the same namespace and name.
    // The CLR keeps the two distinct, so a catch of one must not receive the other.
    public sealed class NamedException : System.Exception
    {
    }

    public static class TwinThrower
    {
        public static void Throw() => throw new NamedException();
    }
}
