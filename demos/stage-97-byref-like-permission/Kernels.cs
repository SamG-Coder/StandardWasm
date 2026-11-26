namespace ByRefPermissionFixture;

public static class Kernels
{
    public static int Run(int value)
    {
        var frame = new Frame();
        return frame.Scalar == null && frame.Reference == null && frame.Value == null
            ? unchecked(value + 23) : -1;
    }

    public static int ReadScalar(Frame frame) => frame.Scalar == null ? 1 : -1;

    public static int RejectBorrowed() => new BorrowedFrame().Value == null ? 1 : -1;
}
