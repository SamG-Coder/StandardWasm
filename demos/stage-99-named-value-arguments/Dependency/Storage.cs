namespace NamedValueDependency
{
    public struct Left
    {
        public int Marker;
    }

    public struct Right
    {
        public int Marker;
    }

    public struct Slot<T> where T : struct
    {
        public T Value;
    }

    public sealed class Frame
    {
        public Slot<Left> Left;
        public Slot<Right> Right;
    }
}
