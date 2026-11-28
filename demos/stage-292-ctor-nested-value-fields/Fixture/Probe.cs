using System;
using System.Runtime.InteropServices;

namespace ConstructorNestedFields
{
    public sealed class Marker { }

    [StructLayout(LayoutKind.Explicit)]
    public struct Word
    {
        [FieldOffset(0)] public long Bits;
        [FieldOffset(0)] public int Low;
        [FieldOffset(4)] public int High;
    }

    public readonly struct Envelope
    {
        private readonly Word word;
        private readonly Marker tag;

        public Envelope(int low, int high, Marker tag)
        {
            word = default;
            this.tag = tag;
            word.High = high;
            word.Low = low;
        }

        public Envelope(int low, Marker tag)
        {
            word = default;
            this.tag = tag;
            word.Low = low;
        }

        public Envelope(long bits, int high, Marker tag)
        {
            word = default;
            this.tag = tag;
            word.Bits = bits;
            word.High = high;
        }

        public long Bits => word.Bits;
        public int Low => word.Low;
        public int High => word.High;
        public Marker Tag => tag;

        public Word WithCopiedLow(int replacement)
        {
            Word copy = word;
            copy.Low = replacement;
            return copy;
        }
    }

    public struct Pair
    {
        public int Left;
        public int Right;
    }

    public readonly struct SequentialEnvelope
    {
        private readonly Pair pair;

        public SequentialEnvelope(int left, int right, bool swapped)
        {
            pair = default;
            if (swapped)
            {
                pair.Left = right;
                pair.Right = left;
            }
            else
            {
                pair.Left = left;
                pair.Right = right;
            }
        }

        public int Left => pair.Left;
        public int Right => pair.Right;
    }

    public static class Probe
    {
        public static int CombinedBytesAndTag()
        {
            var tag = new Marker();
            var value = new Envelope(unchecked((int)0x89ABCDEF), 0x13579BDF, tag);
            return value.Bits == 0x13579BDF89ABCDEFL && value.Low == unchecked((int)0x89ABCDEF) &&
                value.High == 0x13579BDF && ReferenceEquals(value.Tag, tag) ? 101 : -1;
        }

        public static int DefaultUpperBytes()
        {
            var value = new Envelope(-1, new Marker());
            return value.Bits == 0x00000000FFFFFFFFL && value.High == 0 ? 103 : -1;
        }

        public static int WideThenNarrowStore()
        {
            var value = new Envelope(-1L, 0x12345678, new Marker());
            return value.Bits == 0x12345678FFFFFFFFL && value.Low == -1 ? 107 : -1;
        }

        public static int SeparateSnapshots()
        {
            var before = new Marker();
            var after = new Marker();
            var value = new Envelope(13, 17, before);
            var saved = value;
            value = new Envelope(19, 23, after);
            return saved.Low == 13 && saved.High == 17 && ReferenceEquals(saved.Tag, before) &&
                value.Low == 19 && value.High == 23 && ReferenceEquals(value.Tag, after) ? 109 : -1;
        }

        public static int CopyOutsideConstructor()
        {
            var value = new Envelope(29, 31, new Marker());
            var copy = value.WithCopiedLow(37);
            return value.Low == 29 && value.High == 31 && copy.Low == 37 && copy.High == 31 ? 113 : -1;
        }

        public static int SequentialAndConditional()
        {
            var normal = new SequentialEnvelope(41, 43, false);
            var swapped = new SequentialEnvelope(41, 43, true);
            return normal.Left == 41 && normal.Right == 43 && swapped.Left == 43 && swapped.Right == 41 ? 127 : -1;
        }
    }
}
