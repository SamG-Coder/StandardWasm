using AggregateDependency;

namespace AggregateFixture
{
    public static class Kernels
    {
        private static Extent local;

        public static int Initializations() => Fields.Initializations;

        public static int ReadDefault()
        {
            Extent empty = Fields.Empty;
            Border border = Fields.DefaultBorder;
            Extent inner = border.Inner;
            return empty.Width + empty.Height + inner.Width + inner.Height + border.Thickness;
        }

        public static int ReadFixed() => Encode(Fields.Fixed);
        public static int ReadCurrent() => Encode(Fields.Current);
        public static int ReadLocal() => Encode(local);

        public static int WriteCurrent(int width, int height)
        {
            Fields.Current = new Extent(width, height);
            return ReadCurrent();
        }

        public static int MutateCopy(int width)
        {
            Extent copy = Fields.Current;
            copy.Width = width;
            local = copy;
            return Encode(copy);
        }

        public static int SnapshotWhileWriting(int width, int height)
        {
            Extent previous = Fields.Current;
            Fields.Current = new Extent(width, height);
            return Encode(previous);
        }

        public static int WriteNested(int width, int height, int thickness)
        {
            Fields.CurrentBorder = new Border(new Extent(width, height), thickness);
            return ReadNested();
        }

        public static int ReadNested()
        {
            Border border = Fields.CurrentBorder;
            return unchecked(Encode(border.Inner) + border.Thickness);
        }

        private static int Encode(Extent value) => unchecked(value.Width * 31 + value.Height);
    }
}
