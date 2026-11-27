namespace StaticValueLocations;

public static class Probe
{
    private static class Uninitialized
    {
        internal static readonly Item Empty;
    }
    private static readonly Item Full = new Item(new Marker(43), 64);

    private static Item Copy(in Item source) => source;

    public static int Default()
    {
        Item copy = Copy(in Uninitialized.Empty);
        return copy.Owner == null ? copy.Stamp + 101 : -1;
    }

    public static int Initialized()
    {
        Item copy = Copy(in Full);
        return copy.Owner.Value + copy.Stamp;
    }

    public static int Snapshot()
    {
        Item copy = Full;
        copy.Stamp = 17;
        Item addressed = Copy(in Full);
        return addressed.Stamp + addressed.Owner.Value + copy.Stamp;
    }

    public static int Linked()
    {
        Item empty = Copy(in SharedValues.Empty);
        Item full = Copy(in SharedValues.Full);
        return empty.Owner == null ? empty.Stamp + full.Stamp + full.Owner.Value : -1;
    }

    public static int SameLocation() => Initializer.Seen;

    private static class Initializer
    {
        private static readonly Item Current;
        internal static readonly int Seen;

        static Initializer()
        {
            ref readonly Item alias = ref Current;
            Item before = Copy(in alias);
            Current = new Item(new Marker(59), 68);
            Item after = Copy(in alias);
            Item secondAddress = Copy(in Current);
            Seen = before.Owner == null && after.Owner == secondAddress.Owner && before.Stamp == 0
                ? after.Owner.Value + after.Stamp : -1;
        }
    }
}
