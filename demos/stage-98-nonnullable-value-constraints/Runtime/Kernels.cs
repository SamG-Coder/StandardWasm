namespace NullableRuntimeFixture;

public sealed class Frame(int marker)
{
    public bool? Flag;
    public int? Count;
    public int Marker = marker;
}

public sealed class Strict<T> where T : struct { }

public static class Kernels
{
    public static int Run(int marker)
    {
        var frame = new Frame(marker);
        bool? initialFlag = frame.Flag;
        int? initialCount = frame.Count;
        frame.Flag = initialFlag;
        frame.Count = initialCount;
        return frame.Marker;
    }

    public static int RejectNested(Strict<int> value) => value == null ? 1 : -1;
}
