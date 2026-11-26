namespace HandleConstraintDependency
{
    public interface IShape<T> { }
    public interface IInherited<T> : IShape<T> { }
    public interface IOtherShape<T> { }

    public struct Handle : IInherited<Handle>
    {
        public int Marker;
    }

    public struct DifferentArgument : IShape<Handle>
    {
        public int Marker;
    }

    public struct DifferentDefinition : IOtherShape<DifferentDefinition>
    {
        public int Marker;
    }

    public struct ReferenceHandle : IShape<ReferenceHandle>
    {
        public object Wrapper;
    }

    public struct HandleSlot<T> where T : unmanaged, IShape<T>
    {
        public T Value;
    }

    public sealed class Receiver<T> where T : unmanaged, IShape<T>
    {
        public T Value;
    }

    public sealed class Frame
    {
        public HandleSlot<Handle> Slot;
    }
}
