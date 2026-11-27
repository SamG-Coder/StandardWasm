namespace ValueConstraintDependency
{
    public struct Cell<T> where T : struct
    {
        public bool Present;
        public T Value;
    }

    public struct Payload<T>
    {
        public T Value;
    }

    public interface INominalValue { }
    public struct NominalValue : INominalValue { }

    public struct NominalCell<T> where T : struct, INominalValue
    {
        public int Value;
    }

    public sealed class Frame
    {
        public Cell<int> Scalar;
        public Cell<Payload<int>> Aggregate;
    }

    public sealed class ConstrainedReference<T> where T : struct { }
    public sealed class ConstructorOnly<T> where T : new() { }
    public sealed class NominalConstraint<T> where T : NominalBase { }
    public abstract class NominalBase { }
    public sealed class NominalDerived : NominalBase { }
}
