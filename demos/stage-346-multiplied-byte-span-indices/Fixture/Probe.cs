using System;

namespace MultipliedByteSpanFixture
{
    public static class Probe
    {
        public static int ProductAndBias()
        {
            var bytes = new byte[] { 11, 23, 41, 191, 211 };
            return Read(new ReadOnlySpan<byte>(bytes), 2, 2, -1);
        }

        public static int WrapToValidIndex()
        {
            var bytes = new byte[] { 19, 173, 23 };
            return Read(new ReadOnlySpan<byte>(bytes), int.MinValue, 2, 1);
        }

        public static int UnsignedShiftAfterMultiply()
        {
            var bytes = new byte[32];
            bytes[31] = 239;
            return ReadShifted(new ReadOnlySpan<byte>(bytes), uint.MaxValue);
        }

        public static int ProductOutOfBounds()
        {
            var bytes = new byte[] { 17, 41, 191 };
            try { return Read(new ReadOnlySpan<byte>(bytes), -1, 3, 2); }
            catch (IndexOutOfRangeException) { return 607; }
        }

        private static int Read(ReadOnlySpan<byte> span, int value, int scale, int bias) =>
            span[unchecked(value * scale + bias)];

        private static int ReadShifted(ReadOnlySpan<byte> span, uint word) =>
            span[(int)(unchecked(word * 0x07c4acddu) >> 27)];
    }
}
