namespace StaticValueLocations;

public sealed class Marker(int value)
{
    public readonly int Value = value;
}

public struct Item(Marker owner, int stamp)
{
    public Marker Owner = owner;
    public int Stamp = stamp;
}

public static class SharedValues
{
    public static readonly Item Empty;
    public static readonly Item Full = new Item(new Marker(41), 62);
}
