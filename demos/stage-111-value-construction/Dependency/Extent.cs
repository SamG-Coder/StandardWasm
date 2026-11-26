namespace ValueConstructionDependency
{
    // A linked sequential value type, shaped like System.Drawing.Size.
    public struct Extent
    {
        public int Width;
        public int Height;

        public Extent(int width, int height)
        {
            Width = width;
            Height = height;
        }

        // A chained constructor calls the other body on its own receiver.
        public Extent(int side) : this(side, unchecked(side + 1)) { }

        // : this() clears the receiver before the field writes.
        public Extent(int width, int height, int scale) : this()
        {
            Width = unchecked(width * scale);
            Height = unchecked(Width + height);
        }
    }

    // A reference constructor that embeds a linked value.
    public sealed class Panel
    {
        public Extent Size;

        public Panel(int side)
        {
            Size = new Extent(side);
        }
    }

    public static class Borrow
    {
        public static void Touch(ref Extent extent, int value) => extent.Width = value;
    }
}
