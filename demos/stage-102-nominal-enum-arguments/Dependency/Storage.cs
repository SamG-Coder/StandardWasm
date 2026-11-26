namespace EnumArgumentDependency
{
    public enum Left : sbyte { Zero }
    public enum Right : sbyte { Zero }

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
