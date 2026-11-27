using System;

namespace CharacterSpanFieldCopy
{
    public static class Probe
    {
        public static int Overlap()
        {
            var data = new[] { 'a', 'b', 'c', 'd' };
            var writer = new Writer(data, 1, 3);
            var source = new Span<char>(data);
            writer.Copy(source.Slice(0, 3));
            return data[0] == 'a' && data[1] == 'a' && data[2] == 'b' && data[3] == 'c' ? 401 : 0;
        }

        public static int Bounded()
        {
            var data = new[] { 'a', 'b', 'c', 'd' };
            var writer = new Writer(data, 1, 2);
            char[] letters = { 'x', 'y' };
            var source = new Span<char>(letters);
            writer.CopyBounded(source);
            return data[0] == 'a' && data[1] == 'x' && data[2] == 'y' && data[3] == 'd' ? 403 : 0;
        }

        public static int InvalidStart()
        {
            var data = new[] { 'a' };
            var writer = new Writer(data, -1, 0);
            try { writer.Copy(default); }
            catch (ArgumentOutOfRangeException) { return data[0] == 'a' ? 407 : 0; }
            return 0;
        }

        public static int ShortDestination()
        {
            var data = new[] { 'a' };
            var writer = new Writer(data, 1, 0);
            char[] letters = { 'x' };
            var source = new Span<char>(letters);
            try { writer.CopyBounded(source); }
            catch (ArgumentException) { return data[0] == 'a' ? 409 : 0; }
            return 0;
        }

        public static int ArrayDestination()
        {
            char[] letters = { 'p', 'q' };
            var start = new Span<char>(letters);
            ReadOnlySpan<char> source = start;
            char[] target = { 'a', 'b', 'c' };
            source.CopyTo(target);
            return target[0] == 'p' && target[1] == 'q' && target[2] == 'c' ? 419 : 0;
        }

        public static int NullArray()
        {
            char[] target = null;
            ReadOnlySpan<char> source = default;
            source.CopyTo(target);
            Span<char> empty = target;
            return empty.Length == 0 ? 421 : 0;
        }

        public static int WritableSource()
        {
            char[] letters = { 'a', 'b', 'c' };
            var span = new Span<char>(letters);
            Span<char> source = span.Slice(1, 2);
            source.CopyTo(letters);
            return letters[0] == 'b' && letters[1] == 'c' && letters[2] == 'c' ? 431 : 0;
        }
    }

    internal ref struct Writer
    {
        private Span<char> storage;
        private int start;
        private int length;

        internal Writer(char[] data, int start, int length)
        {
            storage = new Span<char>(data);
            this.start = start;
            this.length = length;
        }

        internal void Copy(ReadOnlySpan<char> source) => source.CopyTo(storage.Slice(start));

        internal void CopyBounded(ReadOnlySpan<char> source) => source.CopyTo(storage.Slice(start, length));
    }
}
