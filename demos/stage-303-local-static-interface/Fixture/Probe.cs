using LocalStaticInterface.Contracts;

namespace LocalStaticInterface;

internal interface IFirst
{
    internal static abstract int Read();
}

internal interface ISecond
{
    internal static abstract int Read();
}

internal interface ILocation
{
    internal static abstract ref readonly int Value { get; }
}

internal readonly struct Source : IFirst, ISecond, ILocation, IExternalLocation
{
    private static int storage;
    private static int externalStorage;

    static int IFirst.Read() => 31;
    static int ISecond.Read() => 47;
    static ref readonly int ILocation.Value => ref storage;
    static ref readonly int IExternalLocation.Value => ref externalStorage;

    internal static void Write(int value) => storage = value;
    internal static void WriteExternal(int value) => externalStorage = value;
}

public static class Probe
{
    public static int FirstExplicitSlot() => First<Source>();

    public static int SecondExplicitSlot() => Second<Source>();

    public static int ReadonlyRefTracksStore()
    {
        Source.Write(17);
        ref readonly int location = ref Address<Source>();
        int before = location;
        Source.Write(59);
        return before * 100 + location;
    }

    public static int ExternalReadonlyRefTracksStore()
    {
        Source.WriteExternal(21);
        ref readonly int location = ref ExternalLocation.Address<Source>();
        int before = location;
        Source.WriteExternal(43);
        return before * 100 + location;
    }

    private static int First<T>() where T : IFirst => T.Read();
    private static int Second<T>() where T : ISecond => T.Read();
    private static ref readonly int Address<T>() where T : ILocation => ref T.Value;
}
