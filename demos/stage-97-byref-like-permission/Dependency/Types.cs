namespace ByRefPermissionFixture;

public interface IMatch<in T> where T : allows ref struct { }
public sealed class Item { }
public struct Element<T> { public T Value; }
public ref struct Borrowed<T> { public int Value; }

public sealed class Frame
{
    public IMatch<int> Scalar;
    public IMatch<Item> Reference;
    public IMatch<Element<int>> Value;
}

public sealed class BorrowedFrame
{
    public IMatch<Borrowed<int>> Value;
}
