namespace System
{
    public class Object
    {
        public Object() => throw new InvalidOperationException("The third-party base constructor ran.");
    }
}
