namespace HandleConstraintDependency
{
    public interface IShape<T> { }
    public interface IInherited<T> : IShape<T> { }
    public interface IOtherShape<T> { }
    public interface IUnmanagedShape<T> where T : unmanaged { }

    public class ShapeProvider : IUnmanagedShape<Handle> { }
    public sealed class DerivedShapeProvider : ShapeProvider { }

    public static class ShapeMethods
    {
        public static int Request<TProvider>(int value) where TProvider : IUnmanagedShape<Handle> => value;
    }

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
