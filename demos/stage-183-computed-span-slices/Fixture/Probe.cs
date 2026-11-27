using System;

namespace ComputedSpanSlices
{
    public static class Probe
    {
        public static int InclusiveEnd()
        {
            ReadOnlySpan<char> view = "xAπyz".AsSpan(1);
            return Code(view, 1, 2) == 'π' * 256 + 'y' ? 509 : 0;
        }

        public static int WriteThroughSlice()
        {
            char[] data = { 'a', 'b', 'c', 'd', 'e' };
            var span = new Span<char>(data);
            Span<char> view = Shift(span, 1, 2);
            view[0] = 'x';
            return data[2] == 'x' && data[1] == 'b' && view.Length == 3 ? 521 : 0;
        }

        public static int FieldDestination()
        {
            char[] data = { 'a', 'b', 'c', 'd', 'e' };
            var writer = new Writer(data, 1, 4);
            writer.Write("XY".AsSpan(), 1);
            return data[1] == 'b' && data[2] == 'X' && data[3] == 'Y' && data[4] == 'e' ? 523 : 0;
        }

        public static int RangeFailure()
        {
            try { Code("abcd".AsSpan(), 2, 0); }
            catch (ArgumentOutOfRangeException) { return 541; }
            return 0;
        }

        private static int Code(ReadOnlySpan<char> view, int first, int last)
        {
            ReadOnlySpan<char> slice = view.Slice(first, last - first + 1);
            return slice.Length == 2 ? slice[0] * 256 + slice[1] : 0;
        }

        private static Span<char> Shift(Span<char> view, int first, int second) =>
            view.Slice((first * second) | 0);
    }

    internal ref struct Writer
    {
        private Span<char> destination;
        private int start;
        private int count;

        internal Writer(char[] destination, int start, int count)
        {
            this.destination = new Span<char>(destination);
            this.start = start;
            this.count = count;
        }

        internal void Write(ReadOnlySpan<char> source, int delta) =>
            source.CopyTo(destination.Slice(start + delta, count - delta));
    }
}
