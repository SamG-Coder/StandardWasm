namespace AggregateDependency
{
    public struct Extent
    {
        public int Width;
        public int Height;

        public Extent(int width, int height)
        {
            Width = width;
            Height = height;
        }
    }

    public struct Border
    {
        public Extent Inner;
        public int Thickness;

        public Border(Extent inner, int thickness)
        {
            Inner = inner;
            Thickness = thickness;
        }
    }

    public static class Fields
    {
        public static readonly Extent Empty;
        public static readonly Border DefaultBorder;
        public static readonly Extent Fixed;
        public static Extent Current;
        public static Border CurrentBorder;
        public static int Initializations;

        static Fields()
        {
            Initializations++;
            Fixed = new Extent(7, 4);
            Current = new Extent(3, 8);
        }
    }
}
